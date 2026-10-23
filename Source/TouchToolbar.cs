using UnityEngine;
using Verse;
using RimWorld;

namespace RimTouch
{
    // Captures one contact through release; sliding off or cancellation never clicks.
    public sealed class ToolbarContact
    {
        public int Button = -1;
        private int finger = -1;
        private Vector2 origin;
        private bool moved;
        public bool Captured => finger >= 0;
        public void Cancel() { Button = -1; finger = -1; moved = false; }
        public int Process(TouchPoint point, Rect[] buttons)
        {
            if (point.phase == TouchPhase.Began && !Captured)
            {
                for (int i=0;i<buttons.Length;i++) if (buttons[i].Contains(point.position))
                { Button=i; finger=point.fingerId; origin=point.position; moved=false; break; }
            }
            if (!Captured || point.fingerId != finger) return -1;
            moved |= Vector2.Distance(origin,point.position)>18f;
            if (point.phase == TouchPhase.Canceled) { Cancel(); return -1; }
            if (point.phase != TouchPhase.Ended) return -1;
            int action = !moved && Button<buttons.Length && buttons[Button].Contains(point.position) ? Button : -1;
            Cancel(); return action;
        }
    }

    public static partial class TouchInputDriver
    {
        private static readonly ToolbarContact toolbarContact = new ToolbarContact();
        private static bool touchSelectMode;
        private static bool touchActionMode;
        private static bool HasToolbarTouch => toolbarContact.Captured || (TouchInput.touchCount==1 && IsTouchToolbarPosition(TouchToGui(TouchInput.GetTouch(0).position)));
        private static bool ToolbarVisible => TouchModeEnabled && Find.UIRoot is UIRoot_Play;
        private static bool ToolbarCollapsed => RimTouchMod.Settings?.toolbarCollapsed ?? true;
        private static Rect[] BuildToolbarRects(float screenWidth, float screenHeight, float scale, bool collapsed)
        {
            scale=Mathf.Clamp(scale,0.6f,1.25f);
            float cellWidth=64f*scale, cellHeight=36f*scale, gap=3f;
            int columns=collapsed?1:3, count=collapsed?1:11;
            float width=columns*(cellWidth+gap)-gap;
            float x=Mathf.Max(8f,screenWidth-width-180f);
            float y=Mathf.Max(8f,Mathf.Min(100f,screenHeight-((count+columns-1)/columns)*(cellHeight+gap)-8f));
            Rect[] rects=new Rect[count];
            for(int i=0;i<count;i++) rects[i]=new Rect(x+(i%columns)*(cellWidth+gap),y+(i/columns)*(cellHeight+gap),cellWidth,cellHeight);
            return rects;
        }
        private static Rect[] ToolbarRects() => BuildToolbarRects(UI.screenWidth,UI.screenHeight,RimTouchMod.Settings?.toolbarScale??0.8f,ToolbarCollapsed);
        private static bool IsTouchToolbarPosition(Vector2 gui)
        {
            if (!ToolbarVisible) return false;
            foreach(Rect rect in ToolbarRects()) if(rect.Contains(gui)) return true;
            return false;
        }
        private static bool ProcessTouchToolbar(TouchPoint point)
        {
            if(!ToolbarVisible) { toolbarContact.Cancel(); return false; }
            bool captured=toolbarContact.Captured;
            point=new TouchPoint(point.fingerId,TouchToGui(point.position),point.deltaPosition,point.phase);
            bool collapsed=ToolbarCollapsed;
            int action=toolbarContact.Process(point,ToolbarRects());
            if (action>=0 && collapsed) action=10;
            if(!captured && !toolbarContact.Captured && action<0) return false;
            StopInertia(); StopUiScrollInertia();
            CancelRimWorldDragBox();
            SuppressVanillaMapInputFallout();
            if(action>=0) ExecuteToolbarAction(action);
            if(IsEnded(point)) FinishTouch();
            return true;
        }
        private static void ExecuteToolbarAction(int action)
        {
            switch(action)
            {
                case 10: if(RimTouchMod.Settings!=null) RimTouchMod.Settings.toolbarCollapsed=!ToolbarCollapsed; break;
                case 0: touchSelectMode=false; touchActionMode=false; CollapseToolbar(); break;
                case 1: touchSelectMode=true; touchActionMode=false; CollapseToolbar(); break;
                case 2: touchActionMode=!touchActionMode; touchSelectMode=false; CollapseToolbar(); break;
                case 3:
                    touchActionMode=false; touchSelectMode=false;
                    if(Find.CurrentMap!=null) CancelVanillaMapTool();
                    Find.WindowStack?.Notify_PressedCancel();
                    GetMapSelector(Find.UIRoot)?.ClearSelection();
                    break;
                case 4: ToolbarZoom(true); break;
                case 5: ToolbarZoom(false); break;
                case 6: Find.TickManager?.TogglePaused(); break;
                case 7:
                    if(Find.TickManager!=null)
                        Find.TickManager.CurTimeSpeed=Find.TickManager.CurTimeSpeed==TimeSpeed.Normal ? TimeSpeed.Fast : Find.TickManager.CurTimeSpeed==TimeSpeed.Fast ? TimeSpeed.Superfast : TimeSpeed.Normal;
                    break;
                case 8: Find.WindowStack?.Notify_PressedAccept(); break;
                case 9: Find.WindowStack?.Add(new Dialog_MessageBox("Touch controls\n\nPan: drag the map.\nSelect: drag a selection box; tap Pan to return.\nAction: tap once, then tap a pawn or location to open orders.\nHold a map location: open orders.\nZoom + / -: zoom without pinch or a wheel.\nCancel: cancel a tool or close the current dialog.\nAccept: confirm the current dialog.\nPause / Speed: control game time.\n\nBuild, draft, inventory and work controls use the normal game buttons. Text fields use the touchscreen client's on-screen keyboard.\n\nRemote play requires continuous pointer dragging, not click-only input.","Close")); break;
            }
        }
        private static void CollapseToolbar() { if(RimTouchMod.Settings!=null) RimTouchMod.Settings.toolbarCollapsed=true; }
        private static void ToolbarZoom(bool zoomIn)
        {
            StopInertia();
            if(IsWorldMapActive()) { ApplyWorldPinchZoom(100f,zoomIn?115f:87f); StopInertia(); return; }
            CameraDriver camera=Find.CameraDriver;
            if(camera!=null && TryGetCameraRootPos(camera,out Vector3 pos) && TryGetCameraRootSize(camera,out float size))
                camera.SetRootPosAndSize(pos,Mathf.Clamp(size*(zoomIn?0.87f:1.15f),GetMinZoomSize(camera),GetMaxZoomSize(camera)));
        }
        private static void DrawTouchToolbar()
        {
            if(!ToolbarVisible) return;
            string speed=Find.TickManager?.CurTimeSpeed.ToString()??"Normal";
            string[] labels={"Pan","Select","Action","Cancel","Zoom +","Zoom -","Pause",speed,"Accept","Help","Hide"};
            string[] tips={"Drag to move the map.","Drag a box to select pawns. Tap Pan when finished.","Tap Action, then tap a location to open orders. No right mouse button needed.","Cancel the active tool or close a dialog.","Zoom in.","Zoom out.","Pause or resume.","Cycle normal, fast and fastest speed.","Confirm the current dialog.","Show touchscreen control help.","Collapse to one small button."};
            Rect[] rects=ToolbarRects();
            for(int i=0;i<rects.Length;i++)
            {
                int button=ToolbarCollapsed?10:i;
                bool active=ToolbarCollapsed || button==0&&!touchSelectMode&&!touchActionMode || button==1&&touchSelectMode || button==2&&touchActionMode;
                Widgets.DrawBoxSolid(rects[i],active?new Color(0.12f,0.42f,0.53f,0.96f):new Color(0.10f,0.12f,0.16f,0.96f));
                Widgets.DrawBox(rects[i]);
                var anchor=Text.Anchor; var font=Text.Font; Text.Font=GameFont.Tiny; Text.Anchor=TextAnchor.MiddleCenter;
                string label=ToolbarCollapsed?(touchSelectMode?"Select":touchActionMode?"Action":"Touch"):labels[button];
                Widgets.Label(rects[i],label); Text.Anchor=anchor; Text.Font=font;
                TooltipHandler.TipRegion(rects[i],ToolbarCollapsed?"Open touch controls. Current drag mode: "+(touchSelectMode?"Select":"Pan"):tips[button]);
            }
            Event e=Event.current;
            if(e!=null && e.isMouse && IsTouchToolbarPosition(e.mousePosition)) e.Use();
        }
    }
}
