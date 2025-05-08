using System;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using StbImageSharp;


namespace Axone.SrcManager {

    public sealed class Texture2D : IDisposable {

        /* ---------- public metadata ---------- */
        public int Handle { get; }
        public int Width { get; }
        public int Height { get; }
        public SizedInternalFormat InternalFormat { get; }
        public int Levels { get; }

        /* ---------- ctor: raw pixels ---------- */
        private unsafe Texture2D(ReadOnlySpan<byte> pixels ,
                                 int width ,
                                 int height ,
                                 SizedInternalFormat format ,
                                 PixelFormat pixFormat ,
                                 PixelType pixType ,
                                 int levels ,
                                 bool generateMip) {
            Width = width;
            Height = height;
            Levels = levels;
            InternalFormat = format;

            /* 1) create handle */
            GL.CreateTextures(TextureTarget.Texture2D , 1 , out int tex);
            Handle = tex;

            /* 2) allocate immutable storage */
            GL.TextureStorage2D(Handle , levels , format , width , height);

            /* 3) upload level 0 */
            fixed (byte* p = pixels)
                GL.TextureSubImage2D(Handle , 0 ,
                                     0 , 0 , width , height ,
                                     pixFormat , pixType ,
                                     (nint)p);

            /* 4) mipmap */
            if (generateMip && levels > 1)
                GL.GenerateTextureMipmap(Handle);
        }

        /* ---------- factory: load from file ---------- */
        public static Texture2D LoadFromFile(string path ,
                                             bool useSRGB = false ,
                                             bool genMip = true ,
                                             TextureWrapMode wrap = TextureWrapMode.Repeat ,
                                             TextureMinFilter min = TextureMinFilter.LinearMipmapLinear ,
                                             TextureMagFilter mag = TextureMagFilter.Linear ,
                                             float anisotropy = 16f) {
            using Stream fs = File.OpenRead(path);
            return LoadFromStream(fs , useSRGB , genMip , wrap , min , mag , anisotropy);
        }

        public static Texture2D LoadFromStream(Stream data ,
                                               bool useSRGB = false ,
                                               bool genMip = true ,
                                               TextureWrapMode wrap = TextureWrapMode.Repeat ,
                                               TextureMinFilter min = TextureMinFilter.LinearMipmapLinear ,
                                               TextureMagFilter mag = TextureMagFilter.Linear ,
                                               float anisotropy = 16f) {
            var img = ImageResult.FromStream(data , ColorComponents.RedGreenBlueAlpha);
            var tex = new Texture2D(img.Data , img.Width , img.Height ,
                                    useSRGB ? SizedInternalFormat.Srgb8Alpha8 : SizedInternalFormat.Rgba8 ,
                                    PixelFormat.Rgba , PixelType.UnsignedByte ,
                                    levels: genMip ? CalcMipCount(img.Width , img.Height) : 1 ,
                                    generateMip: genMip);

            tex.SetSamplerState(wrap , min , mag , anisotropy);
            return tex;
        }
        const int GL_TEXTURE_MAX_ANISOTROPY_EXT = 0x84FE;
        const int GL_MAX_TEXTURE_MAX_ANISOTROPY_EXT = 0x84FF;
        /* ---------- sampler state ---------- */
        public void SetSamplerState(TextureWrapMode wrap ,
                                    TextureMinFilter min ,
                                    TextureMagFilter mag ,
                                    float anisotropy = 16f) {
            GL.TextureParameter(Handle , TextureParameterName.TextureWrapS , (int)wrap);
            GL.TextureParameter(Handle , TextureParameterName.TextureWrapT , (int)wrap);
            GL.TextureParameter(Handle , TextureParameterName.TextureMinFilter , (int)min);
            GL.TextureParameter(Handle , TextureParameterName.TextureMagFilter , (int)mag);

            /* ---- 各向异性 ---- */
            float maxAniso = 0f;


            // ① 判定驱动是否支持扩展
            string? ext = GL.GetString(StringName.Extensions);
            if (ext != null && ext.Contains("texture_filter_anisotropic")) {
                maxAniso = GL.GetFloat((GetPName)GL_MAX_TEXTURE_MAX_ANISOTROPY_EXT);
            }

            if (maxAniso > 0f && anisotropy > 1f) {
                float final = MathF.Min(anisotropy , maxAniso);
                GL.TextureParameter(Handle ,
                    (TextureParameterName)All.TextureMaxAnisotropyExt , final);
            }
        }


        /* ---------- bind helpers ---------- */
        public void BindToUnit(TextureUnit unit) {
            GL.BindTextureUnit(unit - TextureUnit.Texture0 , Handle);
        }

        public void BindToUniform(int shaderProg , string uniformName , TextureUnit unit) {
            int loc = GL.GetUniformLocation(shaderProg , uniformName);
            if (loc == -1) throw new ArgumentException($"Uniform {uniformName} not found");
            GL.ProgramUniform1(shaderProg , loc , unit - TextureUnit.Texture0);
            BindToUnit(unit);
        }

        /* ---------- utils ---------- */
        static int CalcMipCount(int w , int h) {
            int levels = 1;
            while ((w | h) >> levels != 0) levels++;
            return levels;
        }

        /* ---------- dispose ---------- */
        public void Dispose() => GL.DeleteTexture(Handle);
    }

}
