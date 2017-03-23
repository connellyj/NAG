//======= Copyright (c) Valve Corporation, All rights reserved. ===============
//
// Purpose: An area that the player can teleport to
//
//=============================================================================

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Valve.VR.InteractionSystem
{
    //-------------------------------------------------------------------------
    public class TeleportAreaTerrain : TeleportMarkerBase
    {

        //-------------------------------------------------
        public override bool ShouldActivate(Vector3 playerPosition)
        {
            return true;
        }


        //-------------------------------------------------
        public override bool ShouldMovePlayer()
        {
            return true;
        }


        //-------------------------------------------------
        public override void Highlight(bool highlight)
        {
        }


        //-------------------------------------------------
        public override void SetAlpha(float tintAlpha, float alphaPercent)
        {
        }


        //-------------------------------------------------
        public override void UpdateVisuals()
        {
        }


        //-------------------------------------------------
        public void UpdateVisualsInEditor()
        {
        }
    }
}
