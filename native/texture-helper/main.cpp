// Wake-Up optional CPU texture preparation. Original project code.
#include <DirectXTex.h>
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstring>
#include <iostream>
#include <stdexcept>
#include <vector>
#ifdef _WIN32
#include <wincodec.h>
#include <io.h>
#include <fcntl.h>
#else
#include <sys/resource.h>
#include <png.h>
#include <jpeglib.h>
#include <setjmp.h>
#endif

namespace {
constexpr uint32_t InputLimit = 16u * 1024 * 1024;
constexpr uint64_t DecodedLimit = 64ull * 1024 * 1024;
constexpr uint32_t OutputLimit = 8u * 1024 * 1024 - 64u * 1024;
constexpr uint64_t MemoryLimit = 256ull * 1024 * 1024;
constexpr uint32_t RawMemoryLimit = 96u * 1024 * 1024;
constexpr uint32_t RawPixelLimit = 16u * 1024 * 1024;
constexpr char Contract[] = "srgb-color-box-straight-alpha-bc1-bc3-v2";
constexpr char QualityContract[] = "srgb-canonical-metadata-area-alpha-bc-rgba-psd-v4";
bool qualityProtocol = false;
struct ColorMetadata {
    bool profile=false,srgb=false,chromaticities=false,cmyk=false;
    uint32_t gamma=0;
    std::array<double,8> xy={.3127,.329,.64,.33,.30,.60,.15,.06};
} colorMetadata;
struct Unsupported : std::runtime_error { using runtime_error::runtime_error; };
void require(bool ok, const char* why) { if (!ok) throw Unsupported(why); }
void check(HRESULT hr, const char* why) { if (FAILED(hr)) throw std::runtime_error(why); }
uint32_t be32(const uint8_t* p) { return (uint32_t(p[0])<<24)|(uint32_t(p[1])<<16)|(uint32_t(p[2])<<8)|p[3]; }
uint32_t be16(const uint8_t* p) { return (uint32_t(p[0])<<8)|p[1]; }
void read(void* p, size_t n) { if (!std::cin.read(static_cast<char*>(p), n)) throw Unsupported("truncated-request"); }
uint32_t read32() { uint8_t b[4]; read(b,4); return b[0]|(uint32_t(b[1])<<8)|(uint32_t(b[2])<<16)|(uint32_t(b[3])<<24); }
void put32(uint32_t n) { char b[4]={char(n),char(n>>8),char(n>>16),char(n>>24)}; std::cout.write(b,4); }
void response(uint32_t status,uint32_t w=0,uint32_t h=0,uint32_t format=0,uint32_t mips=0,uint32_t length=0) {
    std::cout.write(qualityProtocol?"WUTXRES3":"WUTXRES2",8); for(auto v:{status,w,h,format,mips,length}) put32(v);
}
bool power2(uint32_t x) { return x && !(x&(x-1)); }
uint32_t le32(const uint8_t* p) { return p[0]|(uint32_t(p[1])<<8)|(uint32_t(p[2])<<16)|(uint32_t(p[3])<<24); }
void decodeUncompressedDds(const std::vector<uint8_t>& bytes,uint32_t w,uint32_t h,DirectX::ScratchImage& image) {
    require(qualityProtocol,"uncompressed-dds-quality-only");
    uint32_t flags=le32(&bytes[80]),bits=le32(&bytes[88]);
    uint32_t red=le32(&bytes[92]),green=le32(&bytes[96]),blue=le32(&bytes[100]),alpha=le32(&bytes[104]),stride=0;
    bool hasAlpha=(flags&1)!=0,alphaOnly=false;
    if(flags&64) {
        bool rgb=red==255 && green==65280 && blue==16711680,bgr=red==16711680 && green==65280 && blue==255;
        if((rgb||bgr) && bits==(hasAlpha?32u:24u) && (!hasAlpha || alpha==0xff000000))stride=hasAlpha?4:3;
        else if(!hasAlpha && bits==16 && red==63488 && green==2016 && blue==31)stride=2;
        else if(hasAlpha && bits==16 && red==61440 && green==3840 && blue==240 && alpha==15) {
            // GOG IsRgba4444 returns TextureFormat.ARGB4444 and uploads the
            // original ushort. Preserve that actual channel interpretation.
            stride=2;red=3840;green=240;blue=15;alpha=61440;
        } else throw Unsupported("dds-uncompressed-native-layout");
    } else if((flags&0x20002)!=0 && bits==8){stride=1;alphaOnly=true;hasAlpha=true;}
    require(stride!=0,"dds-uncompressed-format");
    uint32_t count=(le32(&bytes[8])&0x20000)?std::max(1u,le32(&bytes[28])):1u;
    uint64_t expected=128;uint32_t x=w,y=h;
    for(uint32_t mip=0;mip<count;++mip){require(mip<14,"dds-mips");expected+=uint64_t(x)*y*stride;
        require(mip+1==count || x>1 || y>1,"dds-mips");x=std::max(1u,x/2);y=std::max(1u,y/2);}
    require(expected==bytes.size(),"dds-tight-payload-length");
    check(image.Initialize2D(DXGI_FORMAT_R8G8B8A8_UNORM,w,h,1,1),"dds-rgba-allocation");
    auto channel=[](uint32_t value,uint32_t mask)->uint8_t {
        unsigned shift=0;while(((mask>>shift)&1)==0)++shift;
        uint32_t maximum=mask>>shift;return uint8_t((((value&mask)>>shift)*255+maximum/2)/maximum);
    };
    const auto target=image.GetImage(0,0,0);
    for(uint32_t row=0;row<h;++row)for(uint32_t column=0;column<w;++column) {
        const auto source=bytes.data()+128+(uint64_t(row)*w+column)*stride;auto output=target->pixels+row*target->rowPitch+column*4;
        if(alphaOnly){output[0]=output[1]=output[2]=255;output[3]=*source;continue;}
        uint32_t value=0;for(uint32_t b=0;b<stride;++b)value|=uint32_t(source[b])<<(b*8);
        output[0]=channel(value,red);output[1]=channel(value,green);output[2]=channel(value,blue);output[3]=hasAlpha?channel(value,alpha):255;
    }
}
void decodeDds(const std::vector<uint8_t>& bytes,uint32_t w,uint32_t h,DirectX::ScratchImage& image) {
    require(bytes.size()>=128 && le32(&bytes[4])==124 && le32(&bytes[76])==32,"dds-header");
    require(le32(&bytes[12])==h && le32(&bytes[16])==w && le32(&bytes[24])<=1,"source-dimensions");
    require(le32(&bytes[112])==0,"dds-cube-volume");
    if((le32(&bytes[80])&4)==0 || le32(&bytes[84])==0){decodeUncompressedDds(bytes,w,h,image);return;}
    require(le32(&bytes[80])==4,"dds-flags");
    uint32_t fourcc=le32(&bytes[84]),format=0,offset=128;
    if(fourcc==0x31545844)format=71; // DXT1
    else if(qualityProtocol && fourcc==0x33545844)format=74; // DXT3/BC2 codec only; GOG native route is not advertised.
    else if(fourcc==0x35545844)format=77; // DXT5, never premultiplied DXT2/4
    else if(fourcc==0x30315844) {
        require(bytes.size()>=148,"dds-dx10-header"); offset=148; format=le32(&bytes[128]);
        require(le32(&bytes[132])==3 && le32(&bytes[136])==0 && le32(&bytes[140])==1,"dds-dimension-or-array");
        uint32_t alpha=le32(&bytes[144]); require(alpha==0 || alpha==1 || alpha==3,"dds-alpha-mode");
    } else throw Unsupported("dds-format");
    require(format==71 || format==72 || qualityProtocol && (format==74 || format==75) || format==77 || format==78 || format==98 || format==99,"dds-format");
    uint32_t count=qualityProtocol && !(le32(&bytes[8])&0x20000)?1u:std::max(1u,le32(&bytes[28])); uint64_t expected=offset;
    uint32_t x=w,y=h;
    for(uint32_t mip=0;mip<count;++mip) {
        require(mip<14,"dds-mips"); expected+=uint64_t((x+3)/4)*((y+3)/4)*(format==71 || format==72?8:16);
        require(mip+1==count || x>1 || y>1,"dds-mips"); x=std::max(1u,x/2); y=std::max(1u,y/2);
    }
    require(expected==bytes.size(),"dds-payload-length");
    if(qualityProtocol) {
        // Decode the validated top level directly. GOG's mip-count flag decides
        // whether remaining authored levels exist; do not let a DDS convenience
        // loader reinterpret a stale count field differently from native.
        DirectX::Image top{};top.width=w;top.height=h;top.format=static_cast<DXGI_FORMAT>(format);
        top.rowPitch=size_t((w+3)/4)*(format==71 || format==72?8:16);top.slicePitch=top.rowPitch*((h+3)/4);
        top.pixels=const_cast<uint8_t*>(bytes.data()+offset);
        check(DirectX::Decompress(top,DXGI_FORMAT_R8G8B8A8_UNORM,image),"dds-decompress");return;
    }
    DirectX::TexMetadata metadata{};
    check(DirectX::GetMetadataFromDDSMemory(bytes.data(),bytes.size(),DirectX::DDS_FLAGS_NONE,metadata),"dds-metadata");
    require(metadata.width==w && metadata.height==h && metadata.arraySize==1 && metadata.depth==1
        && metadata.dimension==DirectX::TEX_DIMENSION_TEXTURE2D && !metadata.IsCubemap() && metadata.mipLevels==count,"dds-metadata-layout");
    DirectX::ScratchImage compressed;
    check(DirectX::LoadFromDDSMemory(bytes.data(),bytes.size(),DirectX::DDS_FLAGS_NONE,nullptr,compressed),"dds-load");
    // Only the top level is decoded. An explicit quality choice regenerates all
    // smaller levels using the selected ordinary sRGB-color policy.
    check(DirectX::Decompress(*compressed.GetImage(0,0,0),DXGI_FORMAT_R8G8B8A8_UNORM,image),"dds-decompress");
}

// Independently inspect metadata before any codec receives the bytes. Deliberately
// narrow: no color-management/EXIF transforms, animation, palettes or unusual depth.
bool inspect(const std::vector<uint8_t>& b,uint32_t w,uint32_t h) {
    if(b.size()>=8 && !std::memcmp(b.data(),"\x89PNG\r\n\x1a\n",8)) {
        bool ihdr=false,idat=false,end=false;
        size_t p=8;
        while(p+12<=b.size()) {
            uint32_t n=be32(&b[p]); require(n<=b.size()-p-12,"png-chunk-length");
            const uint8_t* t=&b[p+4]; const uint8_t* d=&b[p+8];
            if(!std::memcmp(t,"IHDR",4)) {
                require(!ihdr && p==8 && n==13,"png-ihdr"); ihdr=true;
                require(be32(d)==w && be32(d+4)==h,"source-dimensions");
                require(d[8]==8 && (d[9]==0 || d[9]==2 || d[9]==4 || d[9]==6) && d[10]==0 && d[11]==0 && d[12]==0,"png-layout");
            } else if(!std::memcmp(t,"IDAT",4)) { require(ihdr,"png-ihdr-missing"); idat=true; }
            else if(!std::memcmp(t,"IEND",4)) { require(n==0 && p+12==b.size(),"png-end"); end=true; break; }
            else if(!std::memcmp(t,"sRGB",4)) { require(n==1 && d[0]<=3,"png-srgb"); }
            else if(!std::memcmp(t,"gAMA",4)) { require(n==4 && be32(d)==45455,"png-gamma"); }
            else if(!std::memcmp(t,"pHYs",4)) { require(n==9,"png-physical-size"); }
            else if(!std::memcmp(t,"tEXt",4) || !std::memcmp(t,"tIME",4)) { /* non-color metadata */ }
            else throw Unsupported("png-unqualified-metadata");
            p+=size_t(n)+12;
        }
        require(ihdr && idat && end,"png-incomplete"); return true;
    }
    require(b.size()>4 && b[0]==255 && b[1]==216,"source-kind");
    size_t p=2; bool sof=false;
    while(p+4<=b.size()) {
        require(b[p++]==255,"jpeg-marker"); while(p<b.size() && b[p]==255) ++p;
        require(p<b.size(),"jpeg-marker"); uint8_t m=b[p++];
        require(m!=0 && m!=216 && m!=217 && !(m>=208 && m<=215),"jpeg-marker-order");
        require(p+2<=b.size(),"jpeg-segment"); uint32_t n=be16(&b[p]);
        require(n>=2 && n<=b.size()-p,"jpeg-segment-length"); const uint8_t* d=&b[p+2];
        require(!(m>=225 && m<=239),"jpeg-unqualified-metadata");
        if(m==192) {
            require(!sof && n>=11 && d[0]==8 && (d[5]==1 || d[5]==3) && n==8u+3u*d[5],"jpeg-layout");
            require(be16(d+1)==h && be16(d+3)==w,"source-dimensions"); sof=true;
        } else if(m>=193 && m<=207 && m!=196 && m!=200 && m!=204) throw Unsupported("jpeg-nonbaseline");
        if(m==218) { require(sof,"jpeg-sof-missing"); return false; }
        p+=n;
    }
    throw Unsupported("jpeg-incomplete");
}
// Explicit-quality admission mirrors PreparationImageInspection. The legacy
// native/export protocol retains its previous decoder/metadata contract.
bool inspectQuality(const std::vector<uint8_t>& b,uint32_t w,uint32_t h,uint32_t role) {
    if(b.size()>=8 && !std::memcmp(b.data(),"\x89PNG\r\n\x1a\n",8)) {
        bool header=false,pixels=false,palette=false,end=false; uint32_t color=99,depth=0,paletteCount=0,chunks=0;
        for(size_t p=8;p+12<=b.size();) {
            require(++chunks<=4096,"png-chunk-count");
            uint32_t n=be32(&b[p]); require(n<=b.size()-p-12,"png-chunk-length");
            const auto t=&b[p+4],d=&b[p+8];
            auto is=[&](const char* name){return !std::memcmp(t,name,4);};
            if(is("IHDR")) {
                require(!header && p==8 && n==13,"png-ihdr");header=true;
                require(be32(d)==w && be32(d+4)==h,"source-dimensions");depth=d[8];color=d[9];
                bool valid=color==0?(depth==1||depth==2||depth==4||depth==8||depth==16):
                    color==3?(depth==1||depth==2||depth==4||depth==8):
                    (color==2||color==4||color==6)&&(depth==8||depth==16);
                require(valid && d[10]==0 && d[11]==0 && d[12]<=1,"png-layout");
            } else if(!header)throw Unsupported("png-ihdr-missing");
            else if(is("PLTE")) {
                require(!pixels && !palette && n>=3 && n<=768 && n%3==0 && color!=0 && color!=4,"png-palette");
                palette=true;paletteCount=n/3;require(color!=3 || paletteCount<=(1u<<depth),"png-palette-depth");
            } else if(is("tRNS")) {
                require(!pixels && (color==0?n==2:color==2?n==6:color==3?palette && n>=1 && n<=paletteCount:false),"png-transparency");
            } else if(is("IDAT")){require(color!=3 || palette,"png-palette-missing");pixels=true;}
            else if(is("IEND")){require(n==0 && p+12==b.size(),"png-end");end=true;break;}
            else if(is("sRGB")){require(!pixels && n==1 && d[0]<=3 && !colorMetadata.srgb,"png-srgb");colorMetadata.srgb=true;}
            else if(is("gAMA")){require(!pixels && n==4 && be32(d)!=0,"png-gamma");colorMetadata.gamma=be32(d);}
            else if(is("cHRM")) {
                require(!pixels && n==32,"png-chromaticities");colorMetadata.chromaticities=true;
                for(size_t i=0;i<8;++i)colorMetadata.xy[i]=be32(d+i*4)/100000.0;
                for(size_t i=0;i<8;i+=2)require(colorMetadata.xy[i+1]>0 && colorMetadata.xy[i]+colorMetadata.xy[i+1]<=1,"png-chromaticity-coordinate");
            } else if(is("pHYs")){require(n==9,"png-physical-size");}
            else if(is("iCCP")) {
                require(!colorMetadata.profile && !pixels && n>=4 && n<=1024*1024,"png-icc-bound-or-order");
                size_t zero=0;while(zero<std::min(80u,n) && d[zero])++zero;
                require(zero>0 && zero<std::min(80u,n) && zero+2<n && d[zero+1]==0,"png-icc-name-compression");colorMetadata.profile=true;
            }
            else if(!is("tEXt")&&!is("iTXt")&&!is("zTXt")&&!is("tIME")&&!is("eXIf")&&!is("bKGD")&&!is("sBIT"))throw Unsupported("png-unqualified-metadata");
            p+=size_t(n)+12;
        }
        require(header && pixels && end,"png-incomplete");require(!(colorMetadata.profile && colorMetadata.srgb),"png-conflicting-color-declarations");return true;
    }
    require(b.size()>4 && b[0]==255 && b[1]==216,"source-kind");size_t p=2;bool sof=false;uint32_t segments=0,profileParts=0,profileTotal=0,profileBytes=0;bool profileSeen[256]={};
    while(p+4<=b.size()) {
        require(++segments<=4096 && b[p++]==255,"jpeg-marker");while(p<b.size() && b[p]==255)++p;
        require(p<b.size(),"jpeg-marker");uint8_t m=b[p++];
        require(m!=0 && m!=216 && m!=217 && !(m>=208 && m<=215),"jpeg-marker-order");
        require(p+2<=b.size(),"jpeg-segment");uint32_t n=be16(&b[p]);require(n>=2 && n<=b.size()-p,"jpeg-segment-length");const auto d=&b[p+2];
        if(m==226 && n>=14 && !std::memcmp(d,"ICC_PROFILE\0",12)) {
            require(n>=17 && d[12]!=0 && d[13]!=0 && d[12]<=d[13] && !profileSeen[d[12]] && (!profileTotal || profileTotal==d[13]),"jpeg-icc-sequence");
            profileSeen[d[12]]=true;profileTotal=d[13];++profileParts;profileBytes+=n-16;require(profileBytes<=1024*1024,"jpeg-icc-bound");colorMetadata.profile=true;
        }
        if(m==192 || m==194) {
            require(!sof && n>=11 && d[0]==8 && (d[5]==1 || d[5]==3 || d[5]==4) && n==8u+3u*d[5],"jpeg-layout");
            colorMetadata.cmyk=d[5]==4;require(!colorMetadata.cmyk || role==1,"cmyk-jpeg-is-color-not-rgba-mask");
            require(be16(d+1)==h && be16(d+3)==w,"source-dimensions");sof=true;
        } else if(m>=193 && m<=207 && m!=196 && m!=200 && m!=204)throw Unsupported("jpeg-frame-coding");
        if(m==218){require(sof && profileParts==profileTotal,"jpeg-frame-profile-incomplete");return false;}p+=n;
    }
    throw Unsupported("jpeg-incomplete");
}
void boundProcess(uint64_t memoryLimit=MemoryLimit) {
#ifdef _WIN32
    // Limit only this child, never the game/parent. Keep the handle until exit.
    HANDLE job=CreateJobObjectW(nullptr,nullptr); require(job!=nullptr,"memory-limit-job");
    JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits{};
    limits.BasicLimitInformation.LimitFlags=JOB_OBJECT_LIMIT_PROCESS_MEMORY;
    limits.ProcessMemoryLimit=static_cast<SIZE_T>(memoryLimit);
    if(!SetInformationJobObject(job,JobObjectExtendedLimitInformation,&limits,sizeof(limits)) || !AssignProcessToJobObject(job,GetCurrentProcess())) {
        CloseHandle(job); throw Unsupported("memory-limit-unavailable");
    }
#else
    struct rlimit limit { memoryLimit,memoryLimit };
    require(setrlimit(RLIMIT_AS,&limit)==0,"memory-limit-unavailable");
#endif
}
#ifndef _WIN32
struct JpegError { jpeg_error_mgr base; jmp_buf jump; };
void jpegFail(j_common_ptr c) { longjmp(reinterpret_cast<JpegError*>(c->err)->jump,1); }
#endif
void decode(const std::vector<uint8_t>& bytes,bool png,uint32_t w,uint32_t h,DirectX::ScratchImage& image,uint32_t role=0,uint32_t rawKind=0) {
#ifdef _WIN32
    (void)png;
    check(CoInitializeEx(nullptr,COINIT_MULTITHREADED),"wic-initialize");
    // Decode stored pixel orientation through WIC into an explicitly sized RGBA
    // buffer. Explicit quality color gets profile conversion below; masks and
    // the legacy protocol keep raw channel values, without EXIF rotation.
    IWICImagingFactory* factory=nullptr; IWICStream* stream=nullptr;
    IWICBitmapDecoder* decoder=nullptr; IWICBitmapFrameDecode* frame=nullptr;
    IWICFormatConverter* converter=nullptr; IWICColorTransform* transform=nullptr; IWICColorContext* destination=nullptr;
    std::vector<IWICColorContext*> contexts;
    auto cleanup=[&] { if(transform)transform->Release();if(destination)destination->Release();for(auto context:contexts)if(context)context->Release();
        if(converter)converter->Release(); if(frame)frame->Release(); if(decoder)decoder->Release(); if(stream)stream->Release(); if(factory)factory->Release(); CoUninitialize(); };
    try {
        check(CoCreateInstance(CLSID_WICImagingFactory,nullptr,CLSCTX_INPROC_SERVER,IID_PPV_ARGS(&factory)),"wic-factory");
        check(factory->CreateStream(&stream),"wic-stream");
        check(stream->InitializeFromMemory(const_cast<BYTE*>(bytes.data()),static_cast<DWORD>(bytes.size())),"wic-input");
        check(factory->CreateDecoderFromStream(stream,nullptr,WICDecodeMetadataCacheOnDemand,&decoder),"wic-decoder");
        if(rawKind) {
            GUID container{};check(decoder->GetContainerFormat(&container),"wic-container");
            require(IsEqualGUID(container,rawKind==1?GUID_ContainerFormatPng:GUID_ContainerFormatJpeg),"raw-container-kind");
        }
        UINT frames=0; check(decoder->GetFrameCount(&frames),"wic-frames"); require(frames==1,"multiple-frames");
        check(decoder->GetFrame(0,&frame),"wic-frame"); UINT dw=0,dh=0; check(frame->GetSize(&dw,&dh),"wic-size"); require(dw==w && dh==h,"decoder-dimensions");
        check(factory->CreateFormatConverter(&converter),"wic-converter");
        check(converter->Initialize(frame,GUID_WICPixelFormat32bppRGBA,WICBitmapDitherTypeNone,nullptr,0,WICBitmapPaletteTypeCustom),"wic-rgba");
        check(image.Initialize2D(DXGI_FORMAT_R8G8B8A8_UNORM,w,h,1,1),"decoded-allocation");
        auto target=image.GetImage(0,0,0);
        check(converter->CopyPixels(nullptr,static_cast<UINT>(target->rowPitch),static_cast<UINT>(target->slicePitch),target->pixels),"wic-pixels");
        if(qualityProtocol && role==1 && colorMetadata.profile) {
            // WIC's existing color engine converts embedded ICC color into sRGB.
            // Copy only RGB back: alpha is coverage, not a profile color channel.
            UINT count=0;check(frame->GetColorContexts(0,nullptr,&count),"wic-profile-count");require(count>=1 && count<=16,"wic-profile-count");
            contexts.resize(count,nullptr);for(auto& context:contexts)check(factory->CreateColorContext(&context),"wic-profile-create");
            UINT actual=0;check(frame->GetColorContexts(count,contexts.data(),&actual),"wic-profiles");require(actual==count,"wic-profile-count-changed");
            IWICColorContext* source=nullptr;
            for(auto context:contexts) {
                WICColorContextType type{};check(context->GetType(&type),"wic-profile-type");
                if(type==WICColorContextProfile) {
                    require(source==nullptr,"wic-multiple-icc-profiles");UINT size=0;check(context->GetProfileBytes(0,nullptr,&size),"wic-profile-size");
                    require(size>=128 && size<=1024*1024,"wic-profile-size");source=context;
                }
            }
            require(source!=nullptr,"wic-embedded-profile-unavailable");
            check(factory->CreateColorContext(&destination),"wic-srgb-context");check(destination->InitializeFromExifColorSpace(1),"wic-srgb-context");
            check(factory->CreateColorTransformer(&transform),"wic-color-transform");
            check(transform->Initialize(frame,source,destination,GUID_WICPixelFormat32bppRGBA),"wic-profile-to-srgb");
            std::vector<uint8_t> converted(target->slicePitch);
            check(transform->CopyPixels(nullptr,static_cast<UINT>(target->rowPitch),static_cast<UINT>(converted.size()),converted.data()),"wic-color-pixels");
            for(uint32_t y=0;y<h;++y)for(uint32_t x=0;x<w;++x)std::memcpy(target->pixels+y*target->rowPitch+x*4,converted.data()+y*target->rowPitch+x*4,3);
        }
    } catch(...) { cleanup(); throw; }
    cleanup();
#else
    // The shipped quality client is Windows-only. Keep existing Linux v2
    // qualification separate from WIC-managed profiles and channel semantics.
    require(!qualityProtocol || !colorMetadata.profile && colorMetadata.gamma==0 && !colorMetadata.chromaticities && !colorMetadata.cmyk,"quality-color-metadata-requires-windows");
    check(image.Initialize2D(DXGI_FORMAT_R8G8B8A8_UNORM,w,h,1,1),"decoded-allocation");
    const auto target=image.GetImage(0,0,0);
    if(png) {
        png_image pi{}; pi.version=PNG_IMAGE_VERSION;
        require(png_image_begin_read_from_memory(&pi,bytes.data(),bytes.size())!=0,"png-decode-header");
        if(pi.width!=w || pi.height!=h) { png_image_free(&pi); throw Unsupported("decoder-dimensions"); }
        pi.format=PNG_FORMAT_RGBA;
        bool ok=png_image_finish_read(&pi,nullptr,target->pixels,static_cast<png_int_32>(target->rowPitch),nullptr)!=0;
        png_image_free(&pi); require(ok,"png-decode");
    } else {
        jpeg_decompress_struct dc{}; JpegError error{};
        dc.err=jpeg_std_error(&error.base); error.base.error_exit=jpegFail;
        if(setjmp(error.jump)) { jpeg_destroy_decompress(&dc); throw Unsupported("jpeg-decode"); }
        jpeg_create_decompress(&dc); jpeg_mem_src(&dc,bytes.data(),bytes.size());
        jpeg_read_header(&dc,TRUE);
        if(dc.image_width!=w || dc.image_height!=h) { jpeg_destroy_decompress(&dc); throw Unsupported("decoder-dimensions"); }
        dc.out_color_space=JCS_EXT_RGBA; jpeg_start_decompress(&dc);
        while(dc.output_scanline<dc.output_height) { JSAMPROW row=target->pixels+dc.output_scanline*target->rowPitch; jpeg_read_scanlines(&dc,&row,1); }
        jpeg_finish_decompress(&dc); jpeg_destroy_decompress(&dc);
    }
#endif
}
float linear(uint8_t x) { float s=x/255.f; return s<=0.04045f?s/12.92f:std::pow((s+0.055f)/1.055f,2.4f); }
uint8_t srgb(float x) { float s=x<=0.0031308f?12.92f*x:1.055f*std::pow(x,1.f/2.4f)-0.055f; return uint8_t(std::clamp(std::lround(s*255.f),0l,255l)); }
using Matrix=std::array<double,9>;
using Triple=std::array<double,3>;
Triple colorApply(const Matrix& m,const Triple& v){Triple r{};for(size_t y=0;y<3;++y)for(size_t x=0;x<3;++x)r[y]+=m[y*3+x]*v[x];return r;}
Matrix multiply(const Matrix& a,const Matrix& b){Matrix r{};for(size_t y=0;y<3;++y)for(size_t x=0;x<3;++x)for(size_t k=0;k<3;++k)r[y*3+x]+=a[y*3+k]*b[k*3+x];return r;}
Matrix inverse(const Matrix& a) {
    double d=a[0]*(a[4]*a[8]-a[5]*a[7])-a[1]*(a[3]*a[8]-a[5]*a[6])+a[2]*(a[3]*a[7]-a[4]*a[6]);
    require(std::isfinite(d) && std::abs(d)>1e-10,"png-degenerate-chromaticities");
    return {(a[4]*a[8]-a[5]*a[7])/d,(a[2]*a[7]-a[1]*a[8])/d,(a[1]*a[5]-a[2]*a[4])/d,
        (a[5]*a[6]-a[3]*a[8])/d,(a[0]*a[8]-a[2]*a[6])/d,(a[2]*a[3]-a[0]*a[5])/d,
        (a[3]*a[7]-a[4]*a[6])/d,(a[1]*a[6]-a[0]*a[7])/d,(a[0]*a[4]-a[1]*a[3])/d};
}
Matrix rgbToXyz(const std::array<double,8>& xy) {
    Matrix primaries{};
    for(size_t c=0;c<3;++c){double x=xy[2+c*2],y=xy[3+c*2];primaries[c]=x/y;primaries[3+c]=1;primaries[6+c]=(1-x-y)/y;}
    double x=xy[0],y=xy[1];Triple scale=colorApply(inverse(primaries),{x/y,1,(1-x-y)/y});
    for(size_t c=0;c<3;++c){require(scale[c]>0 && std::isfinite(scale[c]),"png-white-outside-primaries");for(size_t r=0;r<3;++r)primaries[r*3+c]*=scale[c];}
    return primaries;
}
void normalizePngColor(const DirectX::Image& image,uint32_t role) {
    if(!qualityProtocol || role!=1 || colorMetadata.profile || colorMetadata.srgb)return;
    constexpr std::array<double,8> standard={.3127,.329,.64,.33,.30,.60,.15,.06};
    bool gamut=colorMetadata.chromaticities && colorMetadata.xy!=standard;
    bool gamma=colorMetadata.gamma!=0 && colorMetadata.gamma!=45455;
    if(!gamut && !gamma)return;
    Matrix transform={1,0,0,0,1,0,0,0,1};
    if(gamut) {
        // Bradford adaptation maps the declared white to D65 before the sRGB
        // primary transform. Alpha never enters a color-space calculation.
        constexpr Matrix bradford={.8951,.2664,-.1614,-.7502,1.7135,.0367,.0389,-.0685,1.0296};
        auto white=[](const std::array<double,8>& xy)->Triple{return {xy[0]/xy[1],1,(1-xy[0]-xy[1])/xy[1]};};
        Triple from=colorApply(bradford,white(colorMetadata.xy)),to=colorApply(bradford,white(standard));
        Matrix diagonal{};for(size_t i=0;i<3;++i){require(from[i]>0,"png-white-adaptation");diagonal[i*3+i]=to[i]/from[i];}
        Matrix adaptation=multiply(multiply(inverse(bradford),diagonal),bradford);
        transform=multiply(multiply(inverse(rgbToXyz(standard)),adaptation),rgbToXyz(colorMetadata.xy));
    }
    for(size_t y=0;y<image.height;++y)for(size_t x=0;x<image.width;++x) {
        auto pixel=image.pixels+y*image.rowPitch+x*4;Triple value{};
        for(size_t c=0;c<3;++c)value[c]=gamma?std::pow(pixel[c]/255.0,100000.0/colorMetadata.gamma):linear(pixel[c]);
        value=colorApply(transform,value);for(size_t c=0;c<3;++c)pixel[c]=srgb(float(std::clamp(value[c],0.0,1.0)));
    }
}
void halve(const DirectX::Image& from,const DirectX::Image& to,uint32_t role=0) {
    for(size_t y=0;y<to.height;++y) for(size_t x=0;x<to.width;++x) {
        if(qualityProtocol) {
            // Integrate every source pixel's covered area, including odd right
            // and bottom edges. Repeated 2x2 sampling drops those NPOT edges.
            double x0=double(x)*from.width/to.width,x1=double(x+1)*from.width/to.width;
            double y0=double(y)*from.height/to.height,y1=double(y+1)*from.height/to.height;
            double rgb[3]={},alpha=0,area=0;
            for(size_t yy=size_t(y0);yy<std::min(size_t(std::ceil(y1)),from.height);++yy)
                for(size_t xx=size_t(x0);xx<std::min(size_t(std::ceil(x1)),from.width);++xx) {
                    double weight=(std::min(x1,double(xx+1))-std::max(x0,double(xx)))
                        *(std::min(y1,double(yy+1))-std::max(y0,double(yy)));
                    auto p=from.pixels+yy*from.rowPitch+xx*4;
                    for(int c=0;c<3;++c)rgb[c]+=weight*(role==2?p[c]:linear(p[c])*p[3]/255.0);
                    alpha+=weight*p[3];area+=weight;
                }
            auto d=to.pixels+y*to.rowPitch+x*4;
            for(int c=0;c<3;++c)d[c]=role==2?uint8_t(std::lround(rgb[c]/area)):srgb(float(alpha?rgb[c]*255.0/alpha:0));
            d[3]=uint8_t(std::lround(alpha/area));continue;
        }
        float rgb[3]={}; uint32_t alpha=0,count=0;
        for(size_t yy=2*y;yy<std::min(2*y+2,from.height);++yy)
            for(size_t xx=2*x;xx<std::min(2*x+2,from.width);++xx) {
                auto p=from.pixels+yy*from.rowPitch+xx*4;
                for(int c=0;c<3;++c)rgb[c]+=role==2?p[c]:linear(p[c])*(role==1?p[3]/255.f:1.f);
                alpha+=p[3]; ++count;
            }
        auto d=to.pixels+y*to.rowPitch+x*4;
        for(int c=0;c<3;++c)d[c]=role==2?uint8_t(std::lround(rgb[c]/count)):
            srgb(role==1?(alpha?rgb[c]*255.f/alpha:0.f):rgb[c]/count);
        d[3]=uint8_t((alpha+count/2)/count);
    }
}
void run() {
    char magic[8]; read(magic,8); require(!std::memcmp(magic,qualityProtocol?"WUTXREQ3":"WUTXREQ2",8),"request-magic");
    uint32_t preset=read32(),mode=read32(),role=qualityProtocol?read32():0,w=read32(),h=read32(),length=read32();
    bool raw=qualityProtocol && mode==2;
    require(!qualityProtocol || (mode==0 || raw) && (role==1 || role==2),"quality-role-or-mode");
    require(preset>=1 && preset<=3,"preset"); require(mode<=1 || raw,"output-mode"); require(length && length<=(raw?DecodedLimit:InputLimit),"input-limit");
    require(w && h && w<=8192 && h<=8192 && (qualityProtocol || mode==1 || power2(w) && power2(h))
        && uint64_t(w)*h*4<=DecodedLimit,"decoded-limit-or-dimensions");
    uint32_t divisor=1u<<(preset-1),ow=std::max(1u,w/divisor),oh=std::max(1u,h/divisor);
    require(qualityProtocol || ow>=4 && oh>=4,"output-dimensions");
    bool rgba=role==2 || qualityProtocol && (ow%4!=0 || oh%4!=0);
    // Bound decoder work independently. A potentially fitting BC1 output may
    // decode within that bound; actual alpha then decides BC1/BC3 admission
    // before allocating the full output mip chain.
    uint64_t worst=0; uint32_t mips=0;
    for(uint32_t x=ow,y=oh;;x=std::max(1u,x/2),y=std::max(1u,y/2)) {
        worst+=rgba?uint64_t(x)*y*4:uint64_t((x+3)/4)*((y+3)/4)*16;
        ++mips; if(x==1 && y==1)break;
    }
    require((rgba?worst:worst/2)+(mode==1?128:0)<=OutputLimit,"output-limit");
    std::vector<uint8_t> bytes(length); read(bytes.data(),length); require(std::cin.peek()==EOF,"request-trailing-data");
    bool dds=bytes.size()>=4 && !std::memcmp(bytes.data(),"DDS ",4); std::cerr<<"decode\n";
    DirectX::ScratchImage decoded;
    if(raw) {
        require(length==uint64_t(w)*h*4,"raw-rgba-layout");
        check(decoded.Initialize2D(DXGI_FORMAT_R8G8B8A8_UNORM,w,h,1,1),"raw-allocation");
        const auto target=decoded.GetImage(0,0,0);
        for(uint32_t y=0;y<h;++y)std::memcpy(target->pixels+y*target->rowPitch,bytes.data()+uint64_t(y)*w*4,w*4);
    } else if(dds) { require(mode==1 || qualityProtocol,"dds-export-only"); decodeDds(bytes,w,h,decoded); }
    else { bool png=qualityProtocol?inspectQuality(bytes,w,h,role):inspect(bytes,w,h); decode(bytes,png,w,h,decoded,role); }
    normalizePngColor(*decoded.GetImage(0,0,0),role);
    bool opaque=true; auto first=decoded.GetImage(0,0,0);
    for(uint32_t y=0;y<h;++y)for(uint32_t x=0;x<w;++x)if(first->pixels[y*first->rowPitch+x*4+3]!=255)opaque=false;
    require((rgba?worst:opaque?worst/2:worst)+(mode==1?128:0)<=OutputLimit,"actual-alpha-output-limit");
    // Raster codecs return top-row-first pixels; flip those before compression.
    // GOG uploads DDS row zero directly as Unity row zero, so DDS must retain its
    // original ordering. Managed PSD mode 2 already supplies Unity row ordering.
    std::vector<uint8_t> row(first->rowPitch);
    for(uint32_t y=0;mode==0 && !dds && y<h/2;++y) {
        auto a=first->pixels+y*first->rowPitch; auto b=first->pixels+(h-y-1)*first->rowPitch;
        std::memcpy(row.data(),a,row.size()); std::memcpy(a,b,row.size()); std::memcpy(b,row.data(),row.size());
    }
    DirectX::ScratchImage levels; check(levels.Initialize2D(DXGI_FORMAT_R8G8B8A8_UNORM,ow,oh,1,mips),"mip-allocation");
    if(preset==3) {
        DirectX::ScratchImage half; check(half.Initialize2D(DXGI_FORMAT_R8G8B8A8_UNORM,std::max(1u,w/2),std::max(1u,h/2),1,1),"half-allocation");
        halve(*first,*half.GetImage(0,0,0),role); halve(*half.GetImage(0,0,0),*levels.GetImage(0,0,0),role);
    } else if(preset==2)halve(*first,*levels.GetImage(0,0,0),role);
    else for(uint32_t y=0;y<h;++y)std::memcpy(levels.GetImage(0,0,0)->pixels+y*levels.GetImage(0,0,0)->rowPitch,first->pixels+y*first->rowPitch,w*4);
    decoded.Release(); bytes.clear(); bytes.shrink_to_fit();
    std::cerr<<"mips\n";
    for(uint32_t mip=1;mip<mips;++mip)halve(*levels.GetImage(mip-1,0,0),*levels.GetImage(mip,0,0),role);
    if(rgba) {
        uint64_t total=0; for(uint32_t mip=0;mip<mips;++mip) {
            const auto im=levels.GetImage(mip,0,0); total+=im->width*im->height*4;
        }
        require(total==worst && total<=OutputLimit,"mask-output-layout");
        response(0,ow,oh,4,mips,static_cast<uint32_t>(total)); // Unity RGBA32
        for(uint32_t mip=0;mip<mips;++mip) {
            const auto im=levels.GetImage(mip,0,0);
            for(size_t y=0;y<im->height;++y)std::cout.write(reinterpret_cast<const char*>(im->pixels+y*im->rowPitch),im->width*4);
        }
        std::cout.flush(); if(!std::cout)throw std::runtime_error("output-pipe"); return;
    }
    DirectX::ScratchImage compressed; std::cerr<<"compress\n";
    check(DirectX::Compress(levels.GetImages(),levels.GetImageCount(),levels.GetMetadata(),opaque?DXGI_FORMAT_BC1_UNORM:DXGI_FORMAT_BC3_UNORM,DirectX::TEX_COMPRESS_DEFAULT,0.5f,compressed),"compression");
    uint64_t total=0; for(uint32_t mip=0;mip<mips;++mip)total+=compressed.GetImage(mip,0,0)->slicePitch;
    require(total<=OutputLimit,"output-limit");
    if(mode==1) {
        DirectX::Blob container;
        check(DirectX::SaveToDDSMemory(compressed.GetImages(),compressed.GetImageCount(),compressed.GetMetadata(),DirectX::DDS_FLAGS_NONE,container),"dds-save");
        require(container.GetBufferSize()<=OutputLimit,"dds-output-limit");
        response(0,ow,oh,opaque?10:12,mips,static_cast<uint32_t>(container.GetBufferSize()));
        std::cout.write(reinterpret_cast<const char*>(container.GetBufferPointer()),container.GetBufferSize());
    } else {
        response(0,ow,oh,opaque?10:12,mips,static_cast<uint32_t>(total));
        for(uint32_t mip=0;mip<mips;++mip) { auto im=compressed.GetImage(mip,0,0); std::cout.write(reinterpret_cast<const char*>(im->pixels),im->slicePitch); }
    }
    std::cout.flush(); if(!std::cout)throw std::runtime_error("output-pipe");
}
#ifdef _WIN32
// Fixture-only consumed-pixel trial. Admission and strict format validation
// remain in the parent; this decoder performs no quality conversion.
void rawResponse(uint32_t id,uint32_t status,uint32_t w=0,uint32_t h=0,uint32_t length=0) {
    std::cout.write("WUTXRAWR",8);
    for(auto value:{id,status,w,h,length?4u:0u,length})put32(value);
}
void runRaw() {
    for(;;) {
        // EOF is normal only between complete requests. Any partial header or
        // body ends the process: never reinterpret its suffix as another frame.
        char magic[8];std::cin.read(magic,8);
        if(std::cin.gcount()==0 && std::cin.eof())return;
        require(std::cin.gcount()==8 && !std::memcmp(magic,"WUTXRAWQ",8),"raw-request-magic-or-truncation");
        uint32_t id=read32(),w=read32(),h=read32(),kind=read32(),length=read32();
        require(length && length<=InputLimit,"raw-input-limit");
        std::vector<uint8_t> bytes(length);read(bytes.data(),length);
        DirectX::ScratchImage decoded;
        uint32_t status=0;
        try {
            // A process is reused, but no image metadata or codec context is.
            colorMetadata=ColorMetadata{};
            require(w && h && w<=8192 && h<=8192 && uint64_t(w)*h*4<=RawPixelLimit,"raw-pixel-limit-or-dimensions");
            require(kind==1 || kind==2,"raw-source-kind");
            require(kind==1 ? bytes.size()>=8 && !std::memcmp(bytes.data(),"\x89PNG\r\n\x1a\n",8)
                : bytes.size()>=3 && bytes[0]==255 && bytes[1]==216 && bytes[2]==255,"raw-source-signature");
            decode(bytes,kind==1,w,h,decoded,0,kind);
        } catch(const Unsupported& e) { std::cerr<<e.what()<<'\n';status=1; }
          catch(const std::exception& e) { std::cerr<<e.what()<<'\n';status=2; }
        if(status)rawResponse(id,status);
        else {
            rawResponse(id,0,w,h,w*h*4);
            const auto pixels=decoded.GetImage(0,0,0);
            // Tight straight RGBA, base level only. Reverse row writes avoid a
            // duplicate output image; native upload/finalization owns the rest.
            for(uint32_t y=h;y>0;--y)
                std::cout.write(reinterpret_cast<const char*>(pixels->pixels+(y-1)*pixels->rowPitch),w*4);
        }
        std::cout.flush();if(!std::cout)throw std::runtime_error("raw-output-pipe");
    }
}
#endif
}
int main(int argc,char** argv) {
#ifdef _WIN32
    _setmode(_fileno(stdin),_O_BINARY); _setmode(_fileno(stdout),_O_BINARY);
#endif
    if(argc!=2)return 2;
#ifdef _WIN32
    if(!std::strcmp(argv[1],"--stdio-raw-v1")) {
        try {
            boundProcess(RawMemoryLimit);
            std::cout.write("WUTXRAWHELP00001",16);put32(RawMemoryLimit);std::cout.flush();
            if(!std::cout)throw std::runtime_error("raw-handshake-pipe");
            runRaw();return 0;
        } catch(const std::exception& e) { std::cerr<<e.what()<<'\n';return 2; }
    }
#endif
    qualityProtocol=!std::strcmp(argv[1],"--stdio-v3");
    if(!qualityProtocol && std::strcmp(argv[1],"--stdio-v2"))return 2;
    try {
        boundProcess(); std::cout.write(qualityProtocol?"WUTXHELPER000003":"WUTXHELPER000002",16);
        const char* contract=qualityProtocol?QualityContract:Contract;
        std::cout.write(contract,std::strlen(contract)); std::cout.flush(); run(); return 0;
    } catch(const Unsupported& e) { std::cerr<<e.what()<<'\n'; response(1); return 1; }
      catch(const std::exception& e) { std::cerr<<e.what()<<'\n'; response(2); return 2; }
}
