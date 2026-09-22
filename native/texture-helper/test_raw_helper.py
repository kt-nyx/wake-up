"""Offline raw protocol checks. Native game fidelity is a separate qualification."""
import argparse
import io
import json
from pathlib import Path
import queue
import struct
import subprocess
import threading
import zlib

from PIL import Image


CAP = 96 * 1024 * 1024
LIMIT = 16 * 1024 * 1024
HANDSHAKE = b"WUTXRAWHELP00001" + struct.pack("<I", CAP)


def chunk(kind, data):
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))


def png(width, height, pixels, extra=b""):
    rows = b"".join(b"\0" + pixels[y * width * 4:(y + 1) * width * 4] for y in range(height))
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
            + extra + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b""))


def request(identity, width, height, kind, data):
    return b"WUTXRAWQ" + struct.pack("<5I", identity, width, height, kind, len(data)) + data


def bottom_first(pixels, width, height):
    return b"".join(pixels[y * width * 4:(y + 1) * width * 4] for y in reversed(range(height)))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--helper", type=Path, required=True)
    parser.add_argument("--receipt", type=Path)
    args = parser.parse_args()
    checks = []
    flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)

    def invoke(frames, expected, success=True):
        result = subprocess.run([str(args.helper), "--stdio-raw-v1"], input=frames,
                                capture_output=True, timeout=30, creationflags=flags)
        assert (result.returncode == 0) == success, (result.returncode, result.stderr)
        assert result.stdout[:20] == HANDSHAKE, result.stdout[:20]
        offset = 20
        for identity, width, height, pixels in expected:
            assert result.stdout[offset:offset + 8] == b"WUTXRAWR", (identity, result.stdout[offset:])
            rid, status, rw, rh, fmt, length = struct.unpack_from("<6I", result.stdout, offset + 8)
            offset += 32
            assert rid == identity, (rid, identity)
            if pixels is None:
                assert status in (1, 2) and (rw, rh, fmt, length) == (0, 0, 0, 0)
            else:
                assert (status, rw, rh, fmt, length) == (0, width, height, 4, len(pixels))
                assert result.stdout[offset:offset + length] == pixels, (identity, "pixel mismatch")
            offset += length
        assert offset == len(result.stdout), (offset, len(result.stdout))
        return result

    pixels = bytes([255, 0, 7, 0, 0, 255, 19, 128, 0, 0, 255, 255,
                    3, 5, 9, 17, 11, 23, 41, 255, 100, 200, 50, 1])
    plain = png(3, 2, pixels)
    expected = bottom_first(pixels, 3, 2)
    invoke(request(17, 3, 2, 1, plain), [(17, 3, 2, expected)])
    checks.append("non-power-of-two exact straight alpha, hidden RGB, orientation and no resize")

    # A differing gamma is metadata, not permission to transform raw channels.
    metadata = png(3, 2, pixels, chunk(b"gAMA", struct.pack(">I", 100000)))
    frames = [request(0xffffffff, 3, 2, 1, metadata), request(0, 3, 2, 2, plain),
              request(91, 3, 2, 1, b"\x89PNG\r\n\x1a\n"), request(92, 3, 2, 1, plain)]
    invoke(b"".join(frames), [(0xffffffff, 3, 2, expected), (0, 0, 0, None),
                               (91, 0, 0, None), (92, 3, 2, expected)])
    checks.append("correlated reuse and state reset after metadata, wrong kind and codec failure")

    cases = [(0, 2, 1), (3, 0, 1), (8193, 2, 1), (3, 8193, 1),
             (2049, 2048, 1), (3, 2, 0), (3, 2, 3), (2, 3, 1)]
    invoke(b"".join(request(i, w, h, kind, plain) for i, (w, h, kind) in enumerate(cases))
           + request(100, 3, 2, 1, plain),
           [(i, 0, 0, None) for i in range(len(cases))] + [(100, 3, 2, expected)])
    checks.append("dimension, pixel-budget, source-kind and actual-size refusals preserve next frame")

    large = bytes([23, 47, 89, 255]) * (8192 * 512)
    invoke(request(103, 8192, 512, 1, png(8192, 512, large)), [(103, 8192, 512, large)])
    checks.append("inclusive 8192 dimension and 16 MiB raw payload bounds")

    for suffix in [b"X", b"WUTXRAWQ", b"BADMAGIC" + bytes(20),
                   b"WUTXRAWQ" + struct.pack("<5I", 8, 3, 2, 1, LIMIT + 1),
                   b"WUTXRAWQ" + struct.pack("<5I", 8, 3, 2, 1, 0),
                   request(8, 3, 2, 1, plain)[:-1]]:
        invoke(request(7, 3, 2, 1, plain) + suffix, [(7, 3, 2, expected)], success=False)
    checks.append("partial headers/bodies, bad magic and invalid lengths terminate without resynchronization")
    invoke(b"", [])
    checks.append("EOF at request boundary exits normally")

    jpeg = io.BytesIO()
    Image.new("L", (3, 2), 128).save(jpeg, format="JPEG", quality=100)
    gray = bytes([128, 128, 128, 255]) * 6
    invoke(request(111, 3, 2, 2, jpeg.getvalue()) + request(112, 3, 2, 1, plain),
           [(111, 3, 2, gray), (112, 3, 2, expected)])
    checks.append("constant grayscale JPEG then PNG reuse (not general JPEG decoder equivalence)")

    process = subprocess.Popen([str(args.helper), "--stdio-raw-v1"], stdin=subprocess.PIPE,
                               stdout=subprocess.PIPE, stderr=subprocess.PIPE, creationflags=flags)
    try:
        handshake = queue.Queue()
        reader = threading.Thread(target=lambda: handshake.put(process.stdout.read(20)), daemon=True)
        reader.start()
        assert handshake.get(timeout=10) == HANDSHAKE
        reader.join(timeout=10)
        # Cancellation belongs to the owner: close only this child and join it.
        # The handshake proves it is initialized and waiting for a request.
        process.kill()
        process.communicate(timeout=10)
        assert process.returncode is not None
    finally:
        if process.poll() is None:
            process.kill()
            process.communicate(timeout=10)
    checks.append("owned child termination and join")
    receipt = {"helper": str(args.helper.resolve()), "protocol": "raw-v1", "processCapBytes": CAP,
               "passed": len(checks), "checks": checks,
               "limits": "Offline protocol checks only; native base/mip equality and malformed acceptance require GOG probes."}
    if args.receipt:
        args.receipt.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(receipt, indent=2))


if __name__ == "__main__":
    main()
