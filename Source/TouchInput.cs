using System;
using System.Collections.Generic;
using UnityEngine;

namespace RimTouch
{
    public readonly struct TouchPoint
    {
        public readonly int fingerId;
        public readonly Vector2 position;
        public readonly Vector2 deltaPosition;
        public readonly TouchPhase phase;
        public TouchPoint(int id, Vector2 position, Vector2 delta, TouchPhase phase)
        {
            fingerId = id; this.position = position; deltaPosition = delta; this.phase = phase;
        }
    }

    // One lifecycle edge per contact per frame preserves quick taps between render frames.
    public sealed class TouchContactBuffer
    {
        private sealed class Contact
        {
            public readonly List<TouchPoint> Pending = new List<TouchPoint>();
            public Vector2 Position;
            public bool Finished;
        }
        private readonly SortedDictionary<int, Contact> contacts = new SortedDictionary<int, Contact>();
        private readonly List<TouchPoint> frame = new List<TouchPoint>();
        public bool FrameCanceled { get; private set; }
        public int Count => frame.Count;
        public TouchPoint this[int index] => frame[index];
        public void Enqueue(int id, Vector2 position, TouchPhase phase)
        {
            if (!contacts.TryGetValue(id, out Contact contact))
            {
                if (phase != TouchPhase.Began) return;
                contact = new Contact { Position = position };
                contacts.Add(id, contact);
            }
            var sample = new TouchPoint(id, position, Vector2.zero, phase);
            int last = contact.Pending.Count - 1;
            if (phase == TouchPhase.Moved && last >= 0 && contact.Pending[last].phase == TouchPhase.Moved)
                contact.Pending[last] = sample;
            else contact.Pending.Add(sample);
        }
        public void NextFrame()
        {
            frame.Clear(); FrameCanceled = false;
            var remove = new List<int>();
            foreach (var entry in contacts)
            {
                Contact contact = entry.Value;
                if (contact.Finished)
                {
                    if (contact.Pending.Count == 0) { remove.Add(entry.Key); continue; }
                    contact.Finished = false;
                }
                var sample = new TouchPoint(entry.Key, contact.Position, Vector2.zero, TouchPhase.Stationary);
                if (contact.Pending.Count > 0)
                {
                    sample = contact.Pending[0]; contact.Pending.RemoveAt(0);
                    if (sample.phase == TouchPhase.Moved)
                        while (contact.Pending.Count > 0 && contact.Pending[0].phase == TouchPhase.Moved)
                        { sample = contact.Pending[0]; contact.Pending.RemoveAt(0); }
                    Vector2 delta = sample.phase == TouchPhase.Began ? Vector2.zero : sample.position - contact.Position;
                    sample = new TouchPoint(entry.Key, sample.position, delta, sample.phase);
                    contact.Position = sample.position;
                    contact.Finished = sample.phase == TouchPhase.Ended || sample.phase == TouchPhase.Canceled;
                    FrameCanceled |= sample.phase == TouchPhase.Canceled;
                }
                frame.Add(sample);
            }
            foreach (int id in remove) contacts.Remove(id);
        }
        public void CancelAll()
        {
            foreach (var entry in contacts)
            {
                entry.Value.Pending.Clear();
                entry.Value.Finished = false;
                entry.Value.Pending.Add(new TouchPoint(entry.Key, entry.Value.Position, Vector2.zero, TouchPhase.Canceled));
            }
        }
        public void Clear() { contacts.Clear(); frame.Clear(); FrameCanceled = false; }
        public static Vector2 ToRenderPosition(float clientX, float clientY, int clientWidth, int clientHeight, int renderWidth, int renderHeight)
        {
            if (clientWidth <= 0 || clientHeight <= 0) return Vector2.zero;
            return new Vector2(clientX * renderWidth / clientWidth, (clientHeight - clientY) * renderHeight / clientHeight);
        }
    }

    public sealed class RemotePointerAdapter
    {
        private bool held, waitForRelease;
        private Vector2 previous;
        public void Reset(bool pressed) { held = false; waitForRelease = pressed; }
        public void Update(TouchContactBuffer buffer, bool pressed, Vector2 position, bool focused, bool pressedThisFrame = false, bool releasedThisFrame = false)
        {
            if (!focused) { buffer.CancelAll(); held = false; waitForRelease = true; return; }
            if (waitForRelease) { if (!pressed) waitForRelease = false; return; }
            if (!pressed && !held && pressedThisFrame && releasedThisFrame)
            {
                buffer.Enqueue(0, position, TouchPhase.Began); buffer.Enqueue(0, position, TouchPhase.Ended);
                previous = position; return;
            }
            if (pressed && !held) buffer.Enqueue(0, position, TouchPhase.Began);
            else if (pressed && held && position != previous) buffer.Enqueue(0, position, TouchPhase.Moved);
            else if (!pressed && held) buffer.Enqueue(0, position, TouchPhase.Ended);
            previous = position; held = pressed;
        }
    }

    public static class TouchInput
    {
        internal static readonly TouchContactBuffer Buffer = new TouchContactBuffer();
        private static readonly RemotePointerAdapter Pointer = new RemotePointerAdapter();
        private static int frameNumber = -1;
        private static bool? remoteMode;
        public static bool ForwardsNativeMouse => RimTouchMod.Settings?.remotePointerMode == true || WindowsTouchBackend.ForwardPromotedMouse;
        public static string Status => RimTouchMod.Settings?.remotePointerMode == true ? "Remote pointer ready - one contact; mouse and wheel input" : WindowsTouchBackend.Status;
        public static int touchCount { get { Prepare(); return Buffer.Count; } }
        public static bool FrameCanceled { get { Prepare(); return Buffer.FrameCanceled; } }
        public static TouchPoint GetTouch(int index) { Prepare(); return Buffer[index]; }
        private static void Prepare()
        {
            if (frameNumber == Time.frameCount) return;
            frameNumber = Time.frameCount;
            bool enabled = RimTouchMod.Settings != null && RimTouchMod.Settings.enableTouchMode;
            if (!enabled) { Shutdown(); return; }
            bool remote = RimTouchMod.Settings.remotePointerMode;
            if (remoteMode != remote)
            {
                WindowsTouchBackend.Detach(); Buffer.Clear();
                Pointer.Reset(Input.GetMouseButton(0)); remoteMode = remote;
            }
            if (remote) Pointer.Update(Buffer, Input.GetMouseButton(0), Input.mousePosition, Application.isFocused, Input.GetMouseButtonDown(0), Input.GetMouseButtonUp(0));
            else WindowsTouchBackend.Poll(true);
            Buffer.NextFrame();
        }
        public static void Shutdown() { WindowsTouchBackend.Detach(); Buffer.Clear(); frameNumber = -1; remoteMode = null; }
    }
}
