"""Small real-helper image checks; Pillow is test-only, never shipped or invoked by the game."""
import argparse
import io
import json
from pathlib import Path
import struct
import subprocess
import zlib

from PIL import Image, ImageCms


def png(width, height, pixels, extra=b""):
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    rows = b"".join(b"\0" + bytes(v for p in pixels[y * width:(y + 1) * width] for v in p) for y in range(height))
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)) + extra + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b"")


def rgb565(n):
    return ((n >> 11) * 255 // 31, ((n >> 5) & 63) * 255 // 63, (n & 31) * 255 // 31)


def decode_blocks(raw, width, height, fmt):
    """Independent BC1/BC3 bit decoder, returning raw bottom-up pixel rows."""
    result = [None] * (width * height)
    p = 0
    for by in range((height + 3) // 4):
        for bx in range((width + 3) // 4):
            alpha = [255] * 16
            if fmt == 12:
                a, b = raw[p:p + 2]
                table = [a, b]
                if a > b:
                    table += [((7 - i) * a + i * b) // 7 for i in range(1, 7)]
                else:
                    table += [((5 - i) * a + i * b) // 5 for i in range(1, 5)] + [0, 255]
                bits = int.from_bytes(raw[p + 2:p + 8], "little")
                alpha = [table[(bits >> (3 * i)) & 7] for i in range(16)]
                p += 8
            c0, c1, bits = struct.unpack_from("<HHI", raw, p)
            p += 8
            a, b = rgb565(c0), rgb565(c1)
            colors = [a, b]
            if c0 > c1 or fmt == 12:
                colors += [tuple((2 * a[c] + b[c]) // 3 for c in range(3)), tuple((a[c] + 2 * b[c]) // 3 for c in range(3))]
            else:
                colors += [tuple((a[c] + b[c]) // 2 for c in range(3)), (0, 0, 0)]
            for i in range(16):
                x, y = bx * 4 + i % 4, by * 4 + i // 4
                if x < width and y < height:
                    result[y * width + x] = (*colors[(bits >> (i * 2)) & 3], alpha[i])
    return result, p


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--helper", type=Path, required=True)
    parser.add_argument("--receipt", type=Path)
    args = parser.parse_args()
    checks = []
    cmyk_samples = []

    def invoke(data, width, height, preset=1, unsupported=False, suffix=b"", export=False, role=0, raw=False):
        version = b"3" if role else b"2"
        fields = [preset, 2 if raw else int(export)] + ([role] if role else []) + [width, height, len(data)]
        request = b"WUTXREQ" + version + struct.pack("<" + "I" * len(fields), *fields) + data + suffix
        child = subprocess.Popen([str(args.helper.resolve()), "--stdio-v" + version.decode()], stdin=subprocess.PIPE,
                                 stdout=subprocess.PIPE, stderr=subprocess.PIPE, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        assert child.stdout.read(16) == b"WUTXHELPER00000" + version, "real handshake"
        contract = b"srgb-canonical-metadata-area-alpha-bc-rgba-psd-v4" if role else b"srgb-color-box-straight-alpha-bc1-bc3-v2"
        assert child.stdout.read(len(contract)) == contract, "exact output contract"
        output, error = child.communicate(request, timeout=20)
        assert output[:8] == b"WUTXRES" + version, error
        status, w, h, fmt, mips, length = struct.unpack_from("<6I", output, 8)
        if unsupported:
            assert status == child.returncode == 1 and output[12:] == bytes(20), (output, error)
            return
        assert status == child.returncode == 0 and len(output) == 32 + length, (output[:32], error)
        assert (w, h) == (max(1, width // (1 << (preset - 1))), max(1, height // (1 << (preset - 1))))
        if export:
            payload = output[32:]
            assert payload[:4] == b"DDS " and struct.unpack_from("<I", payload, 4)[0] == 124
            assert struct.unpack_from("<2I", payload, 12) == (h, w)
            assert payload[84:88] == (b"DXT1" if fmt == 10 else b"DXT5")
            assert struct.unpack_from("<I", payload, 28)[0] == mips
            return payload
        levels, p = [], 32
        while True:
            if fmt == 4:
                count = w * h * 4
                pixels = list(struct.iter_unpack("4B", output[p:p + count]))
            else:
                pixels, count = decode_blocks(output[p:], w, h, fmt)
            levels.append((w, h, pixels)); p += count
            if w == h == 1:
                break
            w, h = max(1, w // 2), max(1, h // 2)
        assert len(levels) == mips and p == len(output), "complete independent mip layout"
        return fmt, levels

    pixels = [(255, 0, 0, 255) if y < 8 else (0, 0, 255, 255) for y in range(16) for x in range(16)]
    image = png(16, 16, pixels)
    if args.receipt:
        args.receipt.parent.mkdir(parents=True, exist_ok=True)
        (args.receipt.parent / "orientation-16.png").write_bytes(image)
    for preset in (1, 2, 3):
        fmt, levels = invoke(image, 16, 16, preset)
        assert fmt == 10
        for w, h, colors in levels[:-1]:
            assert colors[0][2] > 220 and colors[0][0] < 35, "bottom row remains blue at every nonterminal mip"
            assert colors[-1][0] > 220 and colors[-1][2] < 35, "top row remains red at every nonterminal mip"
        final = levels[-1][2][0]
        assert 160 <= final[0] <= 210 and 160 <= final[2] <= 210, "linear-light average, not sRGB midpoint"
        checks.append(f"asymmetric-orientation-full-mips-linear-light-preset-{preset}")

    pixels = [(0, 255, 0, 0 if y < 8 else 255) for y in range(16) for x in range(16)]
    fmt, levels = invoke(png(16, 16, pixels), 16, 16)
    assert fmt == 12 and levels[0][2][0][3] == 255 and levels[0][2][-1][3] == 0
    assert 118 <= levels[-1][2][0][3] <= 138 and levels[-1][2][0][1] > 235
    checks.append("bc3-straight-alpha-transparent-edge-all-mips")

    gray = Image.new("L", (16, 8), 120); stream = io.BytesIO(); gray.save(stream, format="JPEG")
    fmt, levels = invoke(stream.getvalue(), 16, 8)
    assert fmt == 10 and all(max(c[:3]) - min(c[:3]) <= 9 and 108 <= c[0] <= 133 for c in levels[0][2])
    checks.append("baseline-grayscale-jpeg-nonsquare-mips")

    for name, data, width, height, preset, suffix in [
        ("mismatched-dimensions", image, 8, 16, 1, b""),
        ("non-power-of-two", image, 15, 16, 1, b""),
        ("small-half-output", image, 4, 4, 2, b""),
        ("decoded-limit", image, 8192, 8192, 1, b""),
        ("output-limit", image, 4096, 4096, 1, b""),
        ("trailing-request", image, 16, 16, 1, b"extra"),
        ("truncated-png", image[:-12], 16, 16, 1, b""),
        ("invalid-preset", image, 16, 16, 4, b""),
        ("icc-png", png(16,16,pixels,struct.pack(">I", 0)+b"iCCP"+bytes(4)),16,16,1,b""),
        ("exif-jpeg", stream.getvalue()[:2]+b"\xff\xe1\x00\x08Exif\x00\x00"+stream.getvalue()[2:],16,8,1,b""),
    ]:
        invoke(data, width, height, preset, unsupported=True, suffix=suffix); checks.append(name)
    # An idle request represents a blocked read in an owned one-item child.
    # Cancel/reap it before a new invocation, then prove the next child completes.
    # DDS is reusable top-row-first content. Recompressing authored DDS does not
    # flip it a second time, and never changes the original byte buffer.
    dds = invoke(image, 16, 16, export=True)
    assert decode_blocks(dds[128:], 16, 16, 10)[0][0][0] > 220
    original = bytes(dds)
    for preset in (1, 2, 3):
        converted = invoke(dds, 16, 16, preset, export=True)
        width = 16 // (1 << (preset - 1))
        colors, _ = decode_blocks(converted[128:], width, width, 10)
        assert colors[0][0] > 220 and colors[-1][2] > 220
        assert dds == original
        checks.append(f"dds-bc1-top-row-export-preset-{preset}")
    transparent = invoke(png(16,16,pixels),16,16,export=True)
    assert invoke(transparent,16,16,export=True)[84:88] == b"DXT5"
    checks.append("dds-bc3-alpha-export")
    large_alpha = bytearray(transparent[:128])
    struct.pack_into("<4I",large_alpha,12,2304,4096,4096*2304,0)
    struct.pack_into("<I",large_alpha,28,1)
    large_alpha += bytes(4096*2304)
    invoke(large_alpha,4096,2304,unsupported=True,export=True)
    checks.append("bounded-decode-actual-alpha-rejects-bc3-output-overflow")
    # A BC7 mode-6 constant opaque block decoded from an independent bit layout.
    bits = 1 << 6
    position = 7
    for value in (127,127,0,0,0,0,127,127):
        bits |= value << position; position += 7
    bits |= 3 << position
    block = bits.to_bytes(16,"little")
    bc7 = bytearray(dds[:128]); bc7[84:88] = b"DX10"
    struct.pack_into("<I",bc7,28,1)
    bc7 += struct.pack("<5I",99,3,0,1,1) + block * 16
    result = invoke(bc7,16,16,export=True)
    assert decode_blocks(result[128:],16,16,10)[0][0][0] > 220
    checks.append("dds-bc7-cpu-decode-to-bc1")
    npot = png(20,12,[(255,0,0,255)]*240)
    for preset in (1,2):
        converted = invoke(npot,20,12,preset,export=True)
        invoke(converted,20//(1 << (preset-1)),12//(1 << (preset-1)),export=True)
        checks.append(f"non-power-of-two-dds-export-{preset}")
    for name, data in [("dds-truncated",dds[:-1]),("dds-trailing",dds+b"x"),
                       ("dds-cubemap",dds[:112]+struct.pack("<I",0x200)+dds[116:]),
                       ("dds-array",bc7[:140]+struct.pack("<I",2)+bc7[144:]),
                       ("dds-premultiplied",bc7[:144]+struct.pack("<I",2)+bc7[148:]),
                       ("dds-unsupported-format",bc7[:128]+struct.pack("<I",83)+bc7[132:])]:
        invoke(data,16,16,unsupported=True,export=True); checks.append(name)
    invoke(dds,16,16,unsupported=True); checks.append("dds-not-raw-unity-input")
    child = subprocess.Popen([str(args.helper.resolve()), "--stdio-v2"], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                             stderr=subprocess.PIPE, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    assert child.stdout.read(16) == b"WUTXHELPER000002"
    assert child.stdout.read(40) == b"srgb-color-box-straight-alpha-bc1-bc3-v2"
    child.kill(); child.communicate(timeout=5)
    assert child.returncode is not None
    invoke(image, 16, 16)
    checks.append("owned-child-cancel-join-and-next-request")
    # Transparent magenta has no contribution to a green opaque silhouette.
    # Alpha weighting is color-only: mask RGB remains independent from alpha.
    edge = [(0,255,0,255) if x % 2 else (255,0,255,0) for y in range(16) for x in range(16)]
    for preset in (1, 2, 3):
        fmt, levels = invoke(png(16,16,edge),16,16,preset,role=1)
        assert fmt == 12
        for _, _, colors in (levels[1:] if preset == 1 else levels):
            assert all(c[1] > 230 and c[0] < 25 and c[2] < 25 and 115 <= c[3] <= 140 for c in colors)
        checks.append(f"v3-color-alpha-weighted-mips-preset-{preset}")
    channels = [(x * 16, y * 16, (x+y) * 8, 255 if x % 2 else 0) for y in range(16) for x in range(16)]
    bottom_first = [channels[y * 16 + x] for y in reversed(range(16)) for x in range(16)]
    def half_channels(colors, width, height):
        result = []
        for y in range(max(1,height//2)):
            for x in range(max(1,width//2)):
                sample = [colors[yy*width+xx] for yy in range(2*y,min(2*y+2,height)) for xx in range(2*x,min(2*x+2,width))]
                result.append(tuple((sum(p[c] for p in sample)+len(sample)//2)//len(sample) for c in range(4)))
        return result
    for preset in (1, 2, 3):
        fmt, levels = invoke(png(16,16,channels),16,16,preset,role=2)
        assert fmt == 4
        expected, w, h = bottom_first, 16, 16
        for _ in range(preset - 1):
            expected = half_channels(expected,w,h); w,h = max(1,w//2),max(1,h//2)
        for lw,lh,actual in levels:
            assert (lw,lh,actual) == (w,h,expected), "independent mask channel and mip reference"
            expected = half_channels(expected,w,h); w,h = max(1,w//2),max(1,h//2)
        checks.append(f"v3-mask-exact-independent-rgba-channels-preset-{preset}")
    invoke(image,16,16,unsupported=True,role=7); checks.append("v3-unknown-role-refused")
    for role in (1, 2):
        fmt, levels = invoke(dds,16,16,role=role)
        assert fmt == (10 if role == 1 else 4)
        assert levels[0][2][0][0] > 220 and levels[0][2][-1][2] > 220
        checks.append(f"quality-dds-native-upload-row-order-{role}")
    invoke(bc7[:144]+struct.pack("<I",2)+bc7[148:],16,16,unsupported=True,role=1)
    checks.append("v3-dds-premultiplied-refused")
    invoke(image,16,16,unsupported=True,export=True,role=1); checks.append("v3-export-refused")
    def raw_dds(width,height,bits,masks,payload,flags=0x41):
        header=bytearray(128);header[:4]=b"DDS "
        struct.pack_into("<7I",header,4,124,0x100f,height,width,width*(bits//8),0,1)
        struct.pack_into("<8I",header,76,32,flags,0,bits,*masks)
        struct.pack_into("<I",header,108,0x1000)
        return bytes(header)+payload
    for name,bits,masks,top,bottom,expected_top,expected_bottom,flags in [
        ("rgb24",24,(0xff,0xff00,0xff0000,0),bytes([255,0,0]),bytes([0,0,255]),(255,0,0,255),(0,0,255,255),0x40),
        ("bgr24",24,(0xff0000,0xff00,0xff,0),bytes([0,0,255]),bytes([255,0,0]),(255,0,0,255),(0,0,255,255),0x40),
        ("rgba32",32,(0xff,0xff00,0xff0000,0xff000000),bytes([255,0,0,119]),bytes([0,0,255,221]),(255,0,0,119),(0,0,255,221),0x41),
        ("bgra32",32,(0xff0000,0xff00,0xff,0xff000000),bytes([0,0,255,119]),bytes([255,0,0,221]),(255,0,0,119),(0,0,255,221),0x41),
        ("rgb565",16,(0xf800,0x7e0,0x1f,0),struct.pack("<H",0xf800),struct.pack("<H",0x1f),(255,0,0,255),(0,0,255,255),0x40),
        ("native-rgba4444-as-argb",16,(0xf000,0xf00,0xf0,0xf),struct.pack("<H",0x1234),struct.pack("<H",0xabcd),(34,51,68,17),(187,204,221,170),0x41),
        ("native-alpha8",8,(0,0,0,0xff),bytes([17]),bytes([221]),(255,255,255,17),(255,255,255,221),0x2),
    ]:
        source=raw_dds(5,2,bits,masks,top*5+bottom*5,flags)
        fmt,levels=invoke(source,5,2,role=2)
        assert fmt==4 and levels[0][2]==[expected_top]*5+[expected_bottom]*5, name
        invoke(source[:-1],5,2,role=2,unsupported=True)
        checks.append(f"quality-dds-{name}-exact-native-channels-and-rows")
    # BC2 decoding is a helper capability; the shared GOG preview deliberately
    # refuses DXT3 because the actual native startup loader does not support it.
    bc2=bytearray(raw_dds(5,3,0,(0,0,0,0),b"",4));bc2[84:88]=b"DXT3"
    struct.pack_into("<I",bc2,8,0x81007)
    bc2+=bytes([0x77])*8+struct.pack("<HHI",0xf800,0,0)
    bc2+=bytes([0xcc])*8+struct.pack("<HHI",0x1f,0,0)
    fmt,levels=invoke(bc2,5,3,role=2)
    assert fmt==4 and levels[0][2][0]==(255,0,0,119) and levels[0][2][4]==(0,0,255,204)
    checks.append("quality-helper-bc2-codec-only-exact-alpha")
    # Exact pixel checks use RGBA output (a 5x3 top level), independently avoiding
    # block compression error when testing metadata/channel interpretation.
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    def custom_png(width, height, depth, color, rows, metadata=b"", interlace=0):
        return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR",struct.pack(">IIBBBBB",width,height,depth,color,0,0,interlace)) + metadata + chunk(b"IDAT",zlib.compress(rows)) + chunk(b"IEND",b"")
    palette=Image.new("P",(5,3));palette.putpalette([255,0,0,0,255,0]+[0]*762)
    palette.putdata([x%2 for y in range(3) for x in range(5)])
    stream=io.BytesIO();palette.save(stream,format="PNG",bits=1,transparency=bytes([0,255]))
    fmt,levels=invoke(stream.getvalue(),5,3,role=1)
    assert fmt==4 and levels[0][2][0]==(255,0,0,0) and levels[0][2][1]==(0,255,0,255)
    checks.append("quality-indexed-one-bit-palette-transparency")
    artwork=[(x*40,y*80,70,255) for y in range(3) for x in range(5)]
    passes=[]
    for x0,y0,dx,dy in ((0,0,8,8),(4,0,8,8),(0,4,4,8),(2,0,4,4),(0,2,2,4),(1,0,2,2),(0,1,1,2)):
        for y in range(y0,3,dy):
            selected=[artwork[y*5+x] for x in range(x0,5,dx)]
            if selected: passes.append(b"\0"+bytes(v for pixel in selected for v in pixel))
    _,levels=invoke(custom_png(5,3,8,6,b"".join(passes),interlace=1),5,3,role=2)
    assert levels[0][2]==[artwork[y*5+x] for y in reversed(range(3)) for x in range(5)]
    checks.append("quality-adam7-interlaced-png-exact-channels")
    for color,channels in ((0,1),(2,3),(4,2),(6,4)):
        values=[0x8080]*(channels-1)+[0xffff] if channels>1 else [0x8080]
        rows=(b"\0"+struct.pack(">"+"H"*channels,*values)*5)*3
        fmt,levels=invoke(custom_png(5,3,16,color,rows),5,3,role=2)
        assert fmt==4 and len(levels[0][2])==15
        assert abs(levels[0][2][0][0]-128)<=1
        checks.append(f"quality-sixteen-bit-png-color-{color}")
    for color,depth,pixel in ((0,1,b"\xa8"),(0,2,b"\x6c\x00"),(0,4,b"\x48\xc0")):
        _,levels=invoke(custom_png(5,3,depth,color,(b"\0"+pixel)*3),5,3,role=2)
        assert levels[0][2][0][3]==255
        checks.append(f"quality-low-bit-gray-{depth}")
    # The last odd column must contribute to every reduction, rather than vanish.
    odd=[(255 if x==4 else 0,0,0,255) for y in range(3) for x in range(5)]
    for preset in (1,2,3):
        _,levels=invoke(png(5,3,odd),5,3,preset,role=2)
        assert abs(levels[-1][2][0][0]-51)<=1
        checks.append(f"quality-odd-edge-area-average-{preset}")
    # Existing managed PSD decoder supplies these bottom-up raw RGBA rows.
    raw=bytes(v for pixel in [(0,0,255,255)]*5+[(255,0,0,255)]*5 for v in pixel)
    _,levels=invoke(raw,5,2,role=1,raw=True)
    assert levels[0][2][0]==(0,0,255,255) and levels[0][2][-1]==(255,0,0,255)
    checks.append("quality-managed-psd-raw-bottom-up-contract")
    sample=Image.new("RGB",(5,3),(120,70,210))
    for progressive in (False,True):
        stream=io.BytesIO();sample.save(stream,format="JPEG",quality=100,subsampling=0,progressive=progressive,exif=b"Exif\0\0")
        _,levels=invoke(stream.getvalue(),5,3,role=1)
        assert all(abs(levels[0][2][0][i]-value)<=3 for i,value in enumerate((120,70,210)))
        checks.append(f"quality-jpeg-exif-progressive-{progressive}")
    if __import__("os").name=="nt":
        cmyk_baseline = {}
        for progressive in (False,True):
            for name,ink in (("white",(0,0,0,0)),("black",(0,0,0,255)),("cyan",(255,0,0,0)),("magenta",(0,255,0,0))):
                cmyk=Image.new("CMYK",(5,3),ink);stream=io.BytesIO()
                cmyk.save(stream,format="JPEG",quality=100,progressive=progressive)
                fmt,levels=invoke(stream.getvalue(),5,3,role=1)
                pixel=levels[0][2][0]
                assert fmt==4 and pixel[3]==255 and all(p==pixel for p in levels[0][2]), (name,progressive,fmt,pixel)
                # WIC's unprofiled CMYK conversion uses its color policy, not
                # RGB=(255-C,255-M,255-Y)*(255-K)/255. Verify ink/channel meaning
                # and preserve actual values rather than pinning naive cyan.
                if name=="white": assert min(pixel[:3])>=245, pixel
                elif name=="black": assert max(pixel[:3])<=40, pixel
                elif name=="cyan": assert pixel[0]<=10 and pixel[1]>=120 and pixel[2]>=180, pixel
                else: assert pixel[0]>=180 and pixel[1]<=80 and pixel[2]>=80, pixel
                if progressive: assert pixel==cmyk_baseline[name], (name,pixel,cmyk_baseline[name])
                else: cmyk_baseline[name]=pixel
                cmyk_samples.append(dict(color=name,progressive=progressive,input=list(ink),rgba=list(pixel)))
                if name=="cyan": invoke(stream.getvalue(),5,3,role=2,unsupported=True)
            checks.append(f"quality-cmyk-jpeg-color-conversion-and-mask-refusal-{progressive}")
        profile=ImageCms.ImageCmsProfile(ImageCms.createProfile("sRGB")).tobytes()
        for format in ("PNG","JPEG"):
            stream=io.BytesIO();sample.save(stream,format=format,icc_profile=profile,**({"quality":100,"subsampling":0} if format=="JPEG" else {}))
            _,levels=invoke(stream.getvalue(),5,3,role=1)
            assert all(abs(levels[0][2][0][i]-value)<=4 for i,value in enumerate((120,70,210)))
            _,mask=invoke(stream.getvalue(),5,3,role=2)
            assert all(abs(mask[0][2][0][i]-value)<=3 for i,value in enumerate((120,70,210)))
            checks.append(f"quality-{format}-icc-to-srgb-and-mask-bypass")
        gamma=chunk(b"gAMA",struct.pack(">I",100000))
        neutral=png(5,3,[(128,128,128,127)]*15,gamma)
        _,colors=invoke(neutral,5,3,role=1);_,masks=invoke(neutral,5,3,role=2)
        assert all(187<=c<=189 for c in colors[0][2][0][:3]) and colors[0][2][0][3]==127
        assert masks[0][2][0]==(128,128,128,127)
        checks.append("quality-linear-gamma-to-srgb-preserves-alpha-mask-channels")
        # Non-sRGB primaries with D65 white: neutral white remains neutral.
        chroma=chunk(b"cHRM",struct.pack(">8I",31270,32900,64000,33000,21000,71000,15000,6000))
        _,colors=invoke(png(5,3,[(255,255,255,255)]*15,gamma+chroma),5,3,role=1)
        assert all(c>=254 for c in colors[0][2][0][:3])
        checks.append("quality-declared-chromaticities-neutral-white")
    receipt = {"passed": len(checks), "checks": checks, "helper": str(args.helper.resolve()), "cmykSamples": cmyk_samples, "unityRenderingQualified": False}
    if args.receipt:
        args.receipt.parent.mkdir(parents=True, exist_ok=True); args.receipt.write_text(json.dumps(receipt, indent=2)+"\n")
    print(json.dumps(receipt, indent=2))


if __name__ == "__main__":
    main()
