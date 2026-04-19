namespace PlanarReflections5 {

    /*
    * PIDI - Planar Reflections™ 5 - Copyright© 2017-2024
    * PIDI - Planar Reflections is a trademark and copyrighted property of Jorge Pinal Negrete.

    * You cannot sell, redistribute, share nor make public this code, modified or not, in part nor in whole, through any
    * means on any platform except with the purpose of contacting the developers to request support and only when taking
    * all pertinent measures to avoid its release to the public and / or any unrelated third parties.
    * Modifications are allowed only for internal use within the limits of your Unity based projects and cannot be shared,
    * published, redistributed nor made available to any third parties unrelated to Irreverent Software by any means.
    *
    * For more information, contact us at support@irreverent-software.com
    *
    */

    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Rendering;

    [RequireComponent( typeof( UnityEngine.Rendering.Universal.DecalProjector ) )]
    [ExecuteAlways]
    public class PlanarReflectionCasterDecal : MonoBehaviour {

        public static readonly int _reflectionTex = Shader.PropertyToID( "_ReflectionTex" );
        public static readonly int _reflectionDepth = Shader.PropertyToID( "_ReflectionDepth" );
        public static readonly int _reflectionFog = Shader.PropertyToID( "_ReflectionFog" );
        public static readonly int _blurReflectionTex = Shader.PropertyToID( "_BlurReflectionTex" );


        [System.Serializable]
        public struct BlurSettings {

            [System.NonSerialized] public RenderTexture blurredMap;
            [System.NonSerialized] public RenderTexture blurredDepth;
            public bool useBlur;
            public bool forceFakeBlur;
            public int blurPassMode;
            [Range( 0, 1 )] public float blurRadius;
            [Range( 1, 4 )] public int blurDownscale;


        }


        public PlanarReflectionRenderer castFromRenderer;
        public Material BlurMaterial;
        [SerializeField] protected Material originalMaterial;
        public bool castDepth;
        public bool castFog;
        public bool castReflection;
        public BlurSettings blurSettings;

        [SerializeField] protected Material _instMaterial;

        [SerializeField] protected UnityEngine.Rendering.Universal.DecalProjector rend;

        private void OnEnable() {

#if UNITY_EDITOR
            BlurMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>( UnityEditor.AssetDatabase.GUIDToAssetPath( UnityEditor.AssetDatabase.FindAssets( "PlanarReflections5_InternalBlur" )[0] ) );
#endif

            rend = GetComponent<UnityEngine.Rendering.Universal.DecalProjector>();



            blurSettings.blurDownscale = Mathf.Clamp( blurSettings.blurDownscale, 1, 4 );

            if ( rend ) {
                originalMaterial = rend.material;
                _instMaterial = new Material( rend.material );
                rend.material = _instMaterial;
            }


            RenderPipelineManager.beginCameraRendering -= AssignReflections;
            RenderPipelineManager.beginCameraRendering += AssignReflections;
            RenderPipelineManager.endCameraRendering -= BlackReflection;
            RenderPipelineManager.endCameraRendering += BlackReflection;

        }


        private void OnDisable() {

            RenderPipelineManager.beginCameraRendering -= AssignReflections;
            RenderPipelineManager.endCameraRendering -= BlackReflection;

            RenderTexture.ReleaseTemporary( blurSettings.blurredMap );
            RenderTexture.ReleaseTemporary( blurSettings.blurredDepth );


            if ( rend && originalMaterial ) {
                rend.material = originalMaterial;
            }

            if ( _instMaterial ) {
                DestroyImmediate( _instMaterial );
#if UNITY_EDITOR
                UnityEditor.Selection.activeObject = null;
#endif
            }

        }




        void BlackReflection( ScriptableRenderContext context, Camera cam ) {

          
            rend.material.SetTexture( _blurReflectionTex, Texture2D.blackTexture );
            rend.material.SetTexture( _reflectionTex, Texture2D.blackTexture );


        }


        void AssignReflections( ScriptableRenderContext context, Camera cam ) {


            if ( !Application.isPlaying ) {


                blurSettings.blurDownscale = Mathf.Clamp( blurSettings.blurDownscale, 1, 4 );

            }


            rend.material.SetTexture( _blurReflectionTex, Texture2D.blackTexture );
            rend.material.SetTexture( _reflectionTex, Texture2D.blackTexture );


            if ( !castFromRenderer ) {
                return;
            }


            if ( castReflection || castDepth ) {

                Texture rTex = castFromRenderer.GetReflection( cam );



                if ( rTex != Texture2D.blackTexture && blurSettings.useBlur ) {

                    if ( blurSettings.forceFakeBlur ) {
                        RenderTexture.ReleaseTemporary( blurSettings.blurredMap );
                        var rd = new RenderTextureDescriptor( Mathf.Max( rTex.width / ( blurSettings.blurDownscale * 2 ), 16 ), Mathf.Max( rTex.height / ( blurSettings.blurDownscale * 2 ), 16 ), RenderTextureFormat.DefaultHDR, 0 );
                        rd.msaaSamples = 8;
                        blurSettings.blurredMap = RenderTexture.GetTemporary( rd );
                        var quality = BlurMaterial.GetFloat( "_KernelSize" );
                        BlurMaterial.SetFloat( "_KernelSize", 8 );
                        BlurMaterial.SetFloat( "_Radius", ( blurSettings.blurRadius + 0.01f ) * 16 );
                        Graphics.Blit( rTex, blurSettings.blurredMap, BlurMaterial );
                        BlurMaterial.SetFloat( "_KernelSize", quality );
                    }
                    else {

                        var rd = new RenderTextureDescriptor( Mathf.Max( rTex.width / blurSettings.blurDownscale, 1 ), Mathf.Max( rTex.height / blurSettings.blurDownscale, 1 ), RenderTextureFormat.Default, 0 );
                        rd.sRGB = false;

                        if ( !blurSettings.blurredMap ) {
                            blurSettings.blurredMap = RenderTexture.GetTemporary( rd );
                            rd.colorFormat = RenderTextureFormat.Depth;
                            rd.depthBufferBits = 16;
                        }
                        else if ( blurSettings.blurredMap.width != rTex.width / blurSettings.blurDownscale || blurSettings.blurredMap.height != rTex.height / blurSettings.blurDownscale ) {


                            RenderTexture.ReleaseTemporary( blurSettings.blurredMap );
                            RenderTexture.ReleaseTemporary( blurSettings.blurredDepth );

                            rd.depthBufferBits = 0;
                            rd.colorFormat = RenderTextureFormat.Default;
                            blurSettings.blurredMap = RenderTexture.GetTemporary( rd );
                            rd.colorFormat = RenderTextureFormat.Depth;
                            rd.depthBufferBits = 16;
                            blurSettings.blurredDepth = RenderTexture.GetTemporary( rd );
                        }

                        rd.colorFormat = RenderTextureFormat.Default;

                        BlurMaterial.SetFloat( "_Radius", ( blurSettings.blurRadius + 0.01f ) * 8 );
                        var tempRT = RenderTexture.GetTemporary( rd );
                        Graphics.Blit( rTex, blurSettings.blurredMap, BlurMaterial );
                        Graphics.Blit( blurSettings.blurredMap, tempRT, BlurMaterial );
                        Graphics.Blit( tempRT, blurSettings.blurredMap, BlurMaterial );
                        RenderTexture.ReleaseTemporary( tempRT );
                    }

                    if ( blurSettings.blurredMap ) {

                        if ( blurSettings.blurPassMode == 0 ) {
                            rend.material.SetTexture( _reflectionTex, blurSettings.blurredMap );
                        }
                        else {
                            rend.material.SetTexture( _blurReflectionTex, blurSettings.blurredMap );
                            rend.material.SetTexture( _reflectionTex, rTex );
                        }
                    }
                }
                else {
                    rend.material.SetTexture( _blurReflectionTex, rTex );
                    rend.material.SetTexture( _reflectionTex, rTex );
                }

                if ( castDepth && castFromRenderer.Settings.renderDepth ) {
                    rend.material.SetTexture( _reflectionDepth, castFromRenderer.GetReflectionDepth( cam ) );
                }
                else {
                    rend.material.SetTexture( _reflectionDepth, (Texture)Texture2D.whiteTexture );
                }

                if ( castFog && castFromRenderer.Settings.renderFog ) {
                    rend.material.EnableKeyword( "_USE_FOG" );
                    rend.material.SetTexture( _reflectionFog, castFromRenderer.GetReflectionFog( cam ) );
                }
                else {
                    rend.material.DisableKeyword( "_USE_FOG" );
                    rend.material.SetTexture( _reflectionFog, (Texture)Texture2D.blackTexture );
                }
            }
            else {
                rend.material.SetTexture( _blurReflectionTex, (Texture)Texture2D.blackTexture );
                rend.material.SetTexture( _reflectionTex, (Texture)Texture2D.blackTexture );
            }



        }


    }

}