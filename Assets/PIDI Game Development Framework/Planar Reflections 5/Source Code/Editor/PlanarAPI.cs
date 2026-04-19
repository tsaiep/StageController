
#if UNITY_EDITOR
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

namespace PlanarReflections5 {
    using UnityEditor;

    class PlanarAPI {
        [UnityEditor.Callbacks.DidReloadScripts]
        public static void ModifyDefines() {
#if UNITY_2023_3_OR_NEWER
            var defines = PlayerSettings.GetScriptingDefineSymbols( UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup( EditorUserBuildSettings.selectedBuildTargetGroup ) );
#else
        var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup( EditorUserBuildSettings.selectedBuildTargetGroup );        
#endif

            if ( AssetDatabase.FindAssets( "PlanarReflectionsRenderer" ).Length > 0 ) {
                if ( !defines.Contains( "UPDATE_PLANAR3" ) ) {
#if UNITY_2023_3_OR_NEWER
                    PlayerSettings.SetScriptingDefineSymbols( UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup( EditorUserBuildSettings.selectedBuildTargetGroup ), defines + ";UPDATE_PLANAR3" );
#else
                PlayerSettings.SetScriptingDefineSymbolsForGroup( EditorUserBuildSettings.selectedBuildTargetGroup, defines + ";UPDATE_PLANAR3" );
#endif
                }
            }
            else {



                if ( defines.Contains( "UPDATE_PLANAR3" ) ) {
#if UNITY_2023_3_OR_NEWER
                    PlayerSettings.SetScriptingDefineSymbols( UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup( EditorUserBuildSettings.selectedBuildTargetGroup ), defines.Replace( "UPDATE_PLANAR3", "" ) );
#else
                PlayerSettings.SetScriptingDefineSymbolsForGroup( EditorUserBuildSettings.selectedBuildTargetGroup, defines.Replace( "UPDATE_PLANAR3", "" ) );
#endif
                }

                if ( defines.Contains( "PLANAR3_PRO" ) ) {
#if UNITY_2023_3_OR_NEWER
                    PlayerSettings.SetScriptingDefineSymbols( UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup( EditorUserBuildSettings.selectedBuildTargetGroup ), defines.Replace( "PLANAR3_PRO", "" ) );
#else
PlayerSettings.SetScriptingDefineSymbolsForGroup( EditorUserBuildSettings.selectedBuildTargetGroup, defines.Replace( "PLANAR3_PRO", "" ) );
#endif
                }

                if ( defines.Contains( "PLANAR3_HDRP" ) ) {
#if UNITY_2023_3_OR_NEWER
                    PlayerSettings.SetScriptingDefineSymbols( UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup( EditorUserBuildSettings.selectedBuildTargetGroup ), defines.Replace( "PLANAR3_HDRP", "" ) );
#else
PlayerSettings.SetScriptingDefineSymbolsForGroup( EditorUserBuildSettings.selectedBuildTargetGroup, defines.Replace( "PLANAR3_HDRP", "" ) );
#endif
                }

                if ( defines.Contains( "PLANAR3_LWRP" ) ) {
#if UNITY_2023_3_OR_NEWER
                    PlayerSettings.SetScriptingDefineSymbols( UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup( EditorUserBuildSettings.selectedBuildTargetGroup ), defines.Replace( "PLANAR3_LWRP", "" ) );
#else
                PlayerSettings.SetScriptingDefineSymbolsForGroup( EditorUserBuildSettings.selectedBuildTargetGroup, defines.Replace( "PLANAR3_LWRP", "" ) );
#endif
                }

                if ( defines.Contains( "PLANAR3_URP" ) ) {
#if UNITY_2023_3_OR_NEWER
                    PlayerSettings.SetScriptingDefineSymbols( UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup( EditorUserBuildSettings.selectedBuildTargetGroup ), defines.Replace( "PLANAR3_URP", "" ) );
#else
                PlayerSettings.SetScriptingDefineSymbolsForGroup( EditorUserBuildSettings.selectedBuildTargetGroup, defines.Replace( "PLANAR3_URP", "" ) );
#endif
                }
            }
        }



    }

}
#endif