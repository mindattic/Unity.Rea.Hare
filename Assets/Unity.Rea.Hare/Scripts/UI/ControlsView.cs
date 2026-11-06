using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Unity.Rea.Hare.Audio;
using Unity.Rea.Hare.Input;

namespace Unity.Rea.Hare.UI
{
    /// <summary>
    /// The controls screen (U.HARE-T120), a deliberate addition (docs/BIBLE.md V55): the original has no rebinding,
    /// its keys are fixed in each object's key events. Reached from the title's Controls link, it lists every Player,
    /// Meta and Helper action of <see cref="HareControls"/> with its bindings in one control scheme at a time
    /// (Keyboard&amp;Mouse or Gamepad, switched by the tabs at the top). Clicking a binding listens for the next key,
    /// button or stick of that scheme (<see cref="InputActionRebindingExtensions.PerformInteractiveRebinding"/>; Esc
    /// cancels) and saves the result through <see cref="HareBindings"/>, which applies it to every live controls
    /// instance; "Reset to defaults" puts back the original's keys and the stock gamepad layout.
    ///
    /// The title drives it the way it drives its own menu: it hands over the mouse in room pixels (the stage's own
    /// coordinates) with <see cref="Hover"/> and <see cref="Click"/>, and closes it on Esc or Back unless
    /// <see cref="Busy"/>. Player/Aim (the mouse's screen position) is the pointer itself, not a key, so it is not
    /// listed.
    /// </summary>
    public sealed class ControlsView : IDisposable
    {
        public const string ElementName = "controls";

        /// <summary>A scheme's name in the actions asset and its tab's caption.</summary>
        public static readonly (string Group, string Caption)[] Schemes =
        {
            ("Keyboard&Mouse", "Keyboard & Mouse"),
            ("Gamepad", "Gamepad"),
        };

        /// <summary>Actions not listed: they are not something a player presses.</summary>
        static readonly HashSet<string> Unlisted = new HashSet<string> { "Player/Aim" };

        static readonly Color SlotColor = new Color(16f / 255f, 28f / 255f, 52f / 255f, 1f);
        static readonly Color SlotHover = new Color(24f / 255f, 44f / 255f, 78f / 255f, 1f);
        static readonly Color SlotListening = new Color(120f / 255f, 60f / 255f, 10f / 255f, 1f);
        static readonly Color Accent = new Color(90f / 255f, 200f / 255f, 220f / 255f, 1f);
        static readonly Color Dim = new Color(1f, 1f, 1f, 0.45f);

        /// <summary>What a click landed on.</summary>
        public enum ClickResult { Nothing, Handled, Back }

        sealed class Slot
        {
            public InputAction Action;
            public int BindingIndex;
            public VisualElement Box;
            public Label Text;
        }

        readonly HareControls editing;
        readonly VisualElement root;
        readonly VisualElement columns;
        readonly List<Slot> slots = new List<Slot>();
        readonly List<(VisualElement Box, Action OnClick)> hotspots = new List<(VisualElement, Action)>();
        readonly Label hint;
        InputActionRebindingExtensions.RebindingOperation operation;
        Slot listening;
        int endedFrame = -1;
        int scheme;
        bool backPressed;

        /// <summary>The screen's root element.</summary>
        public VisualElement Root => root;

        /// <summary>The scheme shown (an index into <see cref="Schemes"/>).</summary>
        public int Scheme => scheme;

        /// <summary>True while a binding is listening, and on the frame one ended (the key that ended it is not the title's).</summary>
        public bool Busy => operation != null || Time.frameCount <= endedFrame;

        /// <summary>The binding slots shown, for tests: the action, the binding's index and its caption.</summary>
        public IEnumerable<(InputAction Action, int BindingIndex, string Text)> Slots
        {
            get
            {
                foreach (var s in slots) yield return (s.Action, s.BindingIndex, s.Text.text);
            }
        }

        /// <summary>The controls this screen edits (a private <see cref="HareControls"/>, never enabled).</summary>
        public HareControls Editing => editing;

        ControlsView(VisualElement parent, float width, float height)
        {
            editing = HareBindings.Create();

            root = new VisualElement { name = ElementName, pickingMode = PickingMode.Ignore };
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.width = width;
            root.style.height = height;
            root.style.backgroundColor = new Color(0f, 0f, 0f, 0.9f);
            root.style.alignItems = Align.Center;
            root.style.paddingTop = 40;
            parent.Add(root);

            var title = HareUi.Text("Controls", HareTextStyles.MenuTitle.UssClass, root);
            title.style.color = Color.white;
            title.style.marginBottom = 16;

            var tabs = HareUi.El("row", root);
            tabs.style.marginBottom = 18;
            for (int i = 0; i < Schemes.Length; i++)
            {
                int index = i;
                var tab = Hotspot(Schemes[i].Caption, tabs, () => ShowScheme(index));
                tab.name = "tab-" + Schemes[i].Group;
            }

            columns = HareUi.El("row", root);
            columns.style.alignItems = Align.FlexStart;
            columns.style.justifyContent = Justify.Center;
            columns.style.width = width - 80;

            hint = HareUi.Text("", HareTextStyles.MenuText.UssClass, root);
            hint.style.color = Color.white;
            hint.style.marginTop = 16;
            hint.style.marginBottom = 10;

            var footer = HareUi.El("row", root);
            Hotspot("Reset to defaults", footer, ResetToDefaults).name = "reset";
            Hotspot("Back", footer, () => backPressed = true).name = "back";

            ShowScheme(0);
        }

        /// <summary>Builds the screen over <paramref name="parent"/> (a layer in room pixels, at the stage's origin).</summary>
        public static ControlsView Open(VisualElement parent, float width, float height) => new ControlsView(parent, width, height);

        /// <summary>Lists the actions with their bindings in <see cref="Schemes"/>[<paramref name="index"/>].</summary>
        public void ShowScheme(int index)
        {
            CancelListening();
            scheme = Mathf.Clamp(index, 0, Schemes.Length - 1);
            slots.Clear();
            hotspots.RemoveAll(h => h.Box.parent != null && IsUnder(h.Box, columns));
            columns.Clear();

            foreach (var map in new[] { editing.Player.Get(), editing.Helper.Get(), editing.Meta.Get() })
            {
                var column = HareUi.El("col", columns);
                column.style.width = 560;
                column.style.marginLeft = 12;
                column.style.marginRight = 12;
                var heading = HareUi.Text(map.name, HareTextStyles.MenuTitle.UssClass, column);
                heading.style.color = Accent;
                heading.style.marginBottom = 8;

                foreach (var action in map.actions)
                {
                    if (Unlisted.Contains(map.name + "/" + action.name)) continue;
                    AddRow(action, column);
                }
            }

            int i = 0;
            foreach (var (box, _) in hotspots)
                if (box.name != null && box.name.StartsWith("tab-", StringComparison.Ordinal))
                    box.style.borderBottomColor = box.style.borderTopColor = box.style.borderLeftColor = box.style.borderRightColor =
                        i++ == scheme ? Accent : SlotColor;
            SetHint(null);
        }

        void AddRow(InputAction action, VisualElement column)
        {
            var row = HareUi.El("row", column);
            row.style.minHeight = 26;
            row.style.marginBottom = 2;
            var name = HareUi.Text(Spaced(action.name), HareTextStyles.MenuText.UssClass, row);
            name.style.width = 170;
            name.style.color = Color.white;

            string group = Schemes[scheme].Group;
            int shown = 0;
            var bindings = action.bindings;
            for (int b = 0; b < bindings.Count; b++)
            {
                var binding = bindings[b];
                if (binding.isComposite) continue;
                if (!InGroup(binding, group)) continue;
                var slot = new Slot { Action = action, BindingIndex = b };
                slot.Box = Hotspot("", row, () => Listen(slot));
                slot.Box.name = action.actionMap.name + "-" + action.name + "-" + b;
                slot.Text = slot.Box.Q<Label>();
                slots.Add(slot);
                Caption(slot);
                shown++;
            }
            if (shown == 0)
            {
                var none = HareUi.Text("(not bound)", HareTextStyles.MenuText.UssClass, row);
                none.style.color = Dim;
            }
        }

        static bool InGroup(InputBinding binding, string group)
        {
            if (string.IsNullOrEmpty(binding.groups)) return false;
            foreach (var g in binding.groups.Split(InputBinding.Separator))
                if (string.Equals(g, group, StringComparison.Ordinal)) return true;
            return false;
        }

        void Caption(Slot slot)
        {
            var binding = slot.Action.bindings[slot.BindingIndex];
            string text = slot.Action.GetBindingDisplayString(slot.BindingIndex);
            if (string.IsNullOrEmpty(text)) text = "(none)";
            if (binding.isPartOfComposite) text = binding.name + ": " + text;
            if (binding.overridePath != null) text += " *";
            slot.Text.text = text;
            slot.Box.style.backgroundColor = SlotColor;
        }

        /// <summary>A clickable box with a caption, hit-tested by <see cref="Click"/>.</summary>
        VisualElement Hotspot(string caption, VisualElement parent, Action onClick)
        {
            var box = HareUi.El(null, parent);
            box.pickingMode = PickingMode.Ignore;
            box.style.backgroundColor = SlotColor;
            box.style.borderTopWidth = box.style.borderBottomWidth = box.style.borderLeftWidth = box.style.borderRightWidth = 2;
            box.style.borderTopColor = box.style.borderBottomColor = box.style.borderLeftColor = box.style.borderRightColor = SlotColor;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius = box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 6;
            box.style.paddingLeft = box.style.paddingRight = 10;
            box.style.paddingTop = box.style.paddingBottom = 3;
            box.style.marginRight = 6;
            box.style.minWidth = 90;
            box.style.alignItems = Align.Center;
            var text = HareUi.Text(caption, HareTextStyles.MenuText.UssClass, box);
            text.style.color = Color.white;
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            hotspots.Add((box, onClick));
            return box;
        }

        /// <summary>Highlights what the mouse (room pixels) is over.</summary>
        public void Hover(Vector2 roomPixel)
        {
            foreach (var (box, _) in hotspots)
            {
                if (listening != null && box == listening.Box) continue;
                box.style.backgroundColor = Contains(box, roomPixel) ? SlotHover : SlotColor;
            }
        }

        /// <summary>A left click at <paramref name="roomPixel"/>: a tab, a binding, Reset or Back.</summary>
        public ClickResult Click(Vector2 roomPixel)
        {
            if (Busy) return ClickResult.Handled;
            foreach (var (box, onClick) in hotspots.ToArray())
            {
                if (!Contains(box, roomPixel)) continue;
                HareAudio.Play("sfx_click");
                backPressed = false;
                onClick();
                return backPressed ? ClickResult.Back : ClickResult.Handled;
            }
            return ClickResult.Nothing;
        }

        bool Contains(VisualElement box, Vector2 roomPixel)
        {
            if (box.panel == null || box.resolvedStyle.display == DisplayStyle.None) return false;
            var rect = root.WorldToLocal(box.worldBound);
            return rect.Contains(roomPixel);
        }

        static bool IsUnder(VisualElement element, VisualElement ancestor)
        {
            for (var e = element; e != null; e = e.parent)
                if (e == ancestor) return true;
            return false;
        }

        /// <summary>Listens for the next control of the shown scheme and binds it to <paramref name="slot"/>.</summary>
        void Listen(Slot slot)
        {
            CancelListening();
            listening = slot;
            slot.Text.text = "press...";
            slot.Box.style.backgroundColor = SlotListening;
            SetHint("Press the new " + (Schemes[scheme].Group == "Gamepad" ? "button or stick" : "key or mouse button") + " for " + Spaced(slot.Action.name) + ", or Esc to cancel");

            var op = slot.Action.PerformInteractiveRebinding(slot.BindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Mouse>/clickCount")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(o => Finish(true))
                .OnCancel(o => Finish(false));
            if (Schemes[scheme].Group == "Gamepad") op.WithControlsHavingToMatchPath("<Gamepad>");
            else op.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>");
            operation = op;
            op.Start();
        }

        void Finish(bool completed)
        {
            var slot = listening;
            listening = null;
            if (operation != null)
            {
                var done = operation;
                operation = null;
                done.Dispose();
            }
            endedFrame = Time.frameCount;
            if (completed)
            {
                if (!HareBindings.Save(editing)) SetHint("The new binding holds for now but could not be saved");
                else SetHint(null);
            }
            else SetHint(null);
            if (slot != null) Caption(slot);
        }

        void CancelListening()
        {
            if (operation != null) operation.Cancel(); // runs Finish(false)
        }

        void ResetToDefaults()
        {
            CancelListening();
            HareBindings.ResetToDefaults(editing);
            ShowScheme(scheme);
            SetHint("Every control is back on its default");
        }

        void SetHint(string text) =>
            hint.text = text ?? "Click a binding to change it (* marks a changed one). Esc or Back to return.";

        /// <summary>"NextGroundWeapon" as "Next Ground Weapon".</summary>
        static string Spaced(string name)
        {
            var chars = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) chars.Append(' ');
                chars.Append(name[i]);
            }
            return chars.ToString();
        }

        /// <summary>Stops any listening binding, removes the screen and frees its controls.</summary>
        public void Dispose()
        {
            CancelListening();
            root.RemoveFromHierarchy();
            editing.Dispose();
        }
    }
}
