using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using HarmonyLib;
using RimTouch;
using UnityEngine;
using Verse;
int checks=0;
void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
string root=Environment.GetEnvironmentVariable("RIMWORLD_ROOT")??throw new Exception("Set RIMWORLD_ROOT");
AssemblyLoadContext.Default.Resolving+=(c,n)=>{var p=Path.Combine(root,n.Name+".dll");return File.Exists(p)?c.LoadFromAssemblyPath(p):null;};
var buffer=new TouchContactBuffer();
buffer.Enqueue(7,new Vector2(10,20),TouchPhase.Began);buffer.Enqueue(7,new Vector2(10,20),TouchPhase.Ended);
buffer.NextFrame();Check(buffer.Count==1&&buffer[0].phase==TouchPhase.Began,"quick tap retains begin frame");
buffer.NextFrame();Check(buffer.Count==1&&buffer[0].phase==TouchPhase.Ended,"quick tap retains end frame");
buffer.NextFrame();Check(buffer.Count==0,"released contact removed on next frame");
buffer.Enqueue(9,new Vector2(20,30),TouchPhase.Began);buffer.Enqueue(2,new Vector2(50,60),TouchPhase.Began);
buffer.NextFrame();Check(buffer.Count==2&&buffer[0].fingerId==2&&buffer[1].fingerId==9,"two contacts keep stable identity order");
buffer.Enqueue(9,new Vector2(24,37),TouchPhase.Moved);buffer.NextFrame();
var moved=buffer[1];Check(moved.deltaPosition==new Vector2(4,7),"movement delta calculated from previous frame");
Check(buffer[0].phase==TouchPhase.Stationary&&buffer[0].deltaPosition==Vector2.zero,"held contact stays stationary");
buffer.Enqueue(9,new Vector2(26,40),TouchPhase.Moved);buffer.Enqueue(9,new Vector2(30,45),TouchPhase.Moved);buffer.NextFrame();
Check(buffer[1].position==new Vector2(30,45)&&buffer[1].deltaPosition==new Vector2(6,8),"move samples coalesce without lost delta");
buffer.CancelAll();buffer.NextFrame();Check(buffer.FrameCanceled&&buffer[0].phase==TouchPhase.Canceled,"focus loss cancels pending actions");
buffer.NextFrame();Check(buffer.Count==0,"canceled contacts are removed");
buffer.Enqueue(90,Vector2.zero,TouchPhase.Moved);buffer.NextFrame();Check(buffer.Count==0,"unknown move cannot invent a touch down");
Check(TouchContactBuffer.ToRenderPosition(200,100,400,200,800,400)==new Vector2(400,200),"client-to-render scaling supports high DPI and inverted Y");
Check(TouchContactBuffer.ToRenderPosition(0,0,0,0,800,400)==Vector2.zero,"zero-sized client rectangle is safe");
var nativeType=typeof(TouchInput).Assembly.GetType("RimTouch.WindowsTouchBackend+TouchInfo");
Check(Marshal.SizeOf(nativeType)==(IntPtr.Size==8?48:40),"TOUCHINPUT ABI matches Windows pointer size");
var prefs=AccessTools.Field(typeof(Prefs),"data");if(prefs!=null&&prefs.GetValue(null)==null)prefs.SetValue(null,Activator.CreateInstance(prefs.FieldType));
int failed=0, patched=0;var harmony=new Harmony("rimtouch.beta.validation");
foreach(var type in typeof(RimTouchMod).Assembly.GetTypes().Where(t=>t.IsDefined(typeof(HarmonyPatch),false)))
{
 try{var methods=harmony.CreateClassProcessor(type).Patch();patched+=methods.Count;Console.WriteLine("HOOK: "+type.Name+" ("+methods.Count+")");}
 catch(Exception e){failed++;Console.WriteLine("FAIL HOOK: "+type.Name+": "+e.GetBaseException().Message);}
}
Check(failed==0,"all Harmony patch classes apply to installed RimWorld 1.7");
Check(patched>20,"UI, camera and game hooks covered");


foreach(var m in AccessTools.GetDeclaredMethods(typeof(Widgets)).Where(m=>m.Name=="BeginScrollView"))Console.WriteLine("SCROLL API: "+m);

buffer.Clear();buffer.Enqueue(1,Vector2.zero,TouchPhase.Began);buffer.Enqueue(1,Vector2.zero,TouchPhase.Ended);buffer.Enqueue(1,Vector2.one,TouchPhase.Began);buffer.NextFrame();buffer.NextFrame();buffer.NextFrame();
Check(buffer.Count==1&&buffer[0].phase==TouchPhase.Began&&buffer[0].position==Vector2.one,"rapid contact ID reuse preserves next touch down");
buffer.Clear();buffer.Enqueue(1,Vector2.zero,TouchPhase.Began);
for(int i=0;i<10000;i++)buffer.Enqueue(1,new Vector2(i,i),TouchPhase.Moved);
buffer.NextFrame();buffer.NextFrame();Check(buffer[0].position==new Vector2(9999,9999),"long render stall preserves latest move without unbounded move queue");
Check(TouchContactBuffer.ToRenderPosition(0,0,400,200,800,400)==new Vector2(0,400),"top edge maps to render top");
Check(TouchContactBuffer.ToRenderPosition(400,200,400,200,800,400)==new Vector2(800,0),"bottom edge maps to render bottom");


var pointer=new RemotePointerAdapter();buffer.Clear();pointer.Reset(false);
pointer.Update(buffer,true,new Vector2(10,10),true);buffer.NextFrame();Check(buffer[0].phase==TouchPhase.Began,"remote pointer press becomes touch begin");
pointer.Update(buffer,true,new Vector2(20,30),true);buffer.NextFrame();Check(buffer[0].deltaPosition==new Vector2(10,20),"remote pointer drag becomes touch movement");
pointer.Update(buffer,false,new Vector2(20,30),true);buffer.NextFrame();Check(buffer[0].phase==TouchPhase.Ended,"remote pointer release becomes touch end");
buffer.Clear();pointer.Reset(true);pointer.Update(buffer,true,Vector2.zero,true);buffer.NextFrame();Check(buffer.Count==0,"enabling remote mode while pressed waits for release");
pointer.Update(buffer,false,Vector2.zero,true);pointer.Update(buffer,true,Vector2.zero,true);buffer.NextFrame();Check(buffer[0].phase==TouchPhase.Began,"remote pointer resumes after release");
pointer.Update(buffer,true,Vector2.zero,false);buffer.NextFrame();Check(buffer.FrameCanceled,"remote focus loss cancels instead of clicking");


buffer.Clear();pointer.Reset(false);pointer.Update(buffer,false,Vector2.one,true,true,true);buffer.NextFrame();
bool beginRetained=buffer.Count==1&&buffer[0].phase==TouchPhase.Began;buffer.NextFrame();
Check(beginRetained&&buffer.Count==1&&buffer[0].phase==TouchPhase.Ended,"remote press and release within one frame retain a complete tap");
var driver=typeof(RimTouchMod).Assembly.GetType("RimTouch.TouchInputDriver");
var selectorMethod=AccessTools.Method(driver,"GetMapSelector");
Check(selectorMethod.Invoke(null,new object[]{null})==null,"selector lookup tolerates missing UI root");
var entryUi=System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(UIRoot_Entry));
Check(selectorMethod.Invoke(null,new[]{entryUi})==null,"main menu selector lookup avoids invalid play-root cast");
var playUi=System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(RimWorld.UIRoot_Play));
Check(selectorMethod.Invoke(null,new[]{playUi})==null,"selector lookup tolerates map UI not initialized");
AccessTools.Method(driver,"CancelRimWorldDragBox").Invoke(null,null);
Check(true,"drag-box cleanup is safe before game UI exists");
var prime=AccessTools.Method(driver,"ShouldPrimeSelection");
Check(!(bool)prime.Invoke(null,new object[]{true,false,2f,0f}),"remote pause never arms box selection");
Check((bool)prime.Invoke(null,new object[]{true,true,0f,0f}),"remote Shift explicitly arms box selection");
Check((bool)prime.Invoke(null,new object[]{false,false,0.4f,0f}),"native touch retains hold-to-select");
Check(!(bool)prime.Invoke(null,new object[]{false,false,0.1f,0f}),"quick native touch remains a tap or pan");
Console.WriteLine($"RESULT: {checks} checks passed; {patched} hooks applied");
