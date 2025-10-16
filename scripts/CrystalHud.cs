using Godot;
using System;
using System.Collections.Generic;

public partial class CrystalHud : Control
{
	[Export] public NodePath IconsContainerPath { get; set; } = null;

	// .tscn ของคริสตัลแต่ละสี
	[Export] public PackedScene BlueScene  { get; set; }
	[Export] public PackedScene GreenScene { get; set; }
	[Export] public PackedScene PinkScene  { get; set; }
	[Export] public PackedScene RedScene   { get; set; }
	[Export] public PackedScene PurpleScene{ get; set; }

	[Export] public Vector2I IconSize = new Vector2I(64,64);
	[Export] public float Node2DScale = 0.5f;
	
	[Export] public int TopPadding = 5;
	
	[Export] public Label CrystalCountLabel { get; set; }   // drag ใน Inspector
	
	private HBoxContainer _box;

	private class Entry
	{
		public CrystalType Type;
		public Control CardRoot; // VBoxContainer
		public Label Label;      // แสดงเวลา / จำนวนสแตก
		public float TimeLeft;   // <0 = no timer (ใช้กับเขียว)
		public int Count;        // ใช้กับเขียว (จำนวนสแตก)
	}
	private readonly Dictionary<CrystalType, Entry> _entries = new();

	public override void _Ready()
{
	_box = (IconsContainerPath != null && !IconsContainerPath.IsEmpty)
		 ? GetNodeOrNull<HBoxContainer>(IconsContainerPath)
		 : GetNodeOrNull<HBoxContainer>("HBox");

	if (_box == null)
	{
		_box = new HBoxContainer { Name = "HBox" };
		AddChild(_box);
	}

	MouseFilter = MouseFilterEnum.Ignore;
	ProcessMode = ProcessModeEnum.Always;
	Position = new Vector2(Position.X, Mathf.Max(TopPadding, Position.Y));

	// ▼ ย้ายจาก _Ready() ตัวบนมาไว้ที่นี่
	if (CrystalCountLabel != null)
		CrystalCountLabel.Position += new Vector2(0, -20);
		
}


	public override void _Process(double delta)
{
	var remove = new List<CrystalType>();
	foreach (var kv in _entries)
	{
		var e = kv.Value;
		if (e.TimeLeft >= 0f)
		{
			e.TimeLeft -= (float)delta;
			if (e.TimeLeft <= 0f) remove.Add(e.Type);
			else
			{
				if (e.Type == CrystalType.Purple)
					e.Label.Text = $"*{Math.Max(1, e.Count)} {Mathf.Ceil(e.TimeLeft)}";
				else
					e.Label.Text = $"{Mathf.Ceil(e.TimeLeft)}";
			}
		}
		// เขียวไม่มีเวลา → ใช้ Count อย่างเดียว
	}
	foreach (var t in remove) ClearBuff(t);
}

	public void ShowBuff(CrystalType type, float durationSec = -1f, string labelOverride = null, int? countOverride = null)
{
	if (_entries.TryGetValue(type, out var exist))
	{
		if (type == CrystalType.Green && durationSec < 0f)
		{
			exist.Count++;                           // ✅ บวกจำนวน
			exist.Label.Text = $"x{exist.Count}";    // ✅ แสดง xN
			return;
		}
		else
		{
			exist.TimeLeft = durationSec;
			// Purple ต้องแสดงรูปแบบ *N time
			if (type == CrystalType.Purple && durationSec >= 0f)
			{
				exist.Count = Math.Max(1, exist.Count);
				exist.Label.Text = labelOverride ?? $"*{exist.Count} {Mathf.Ceil(durationSec)}";
			}
			else
			{
				exist.Label.Text = labelOverride ?? (durationSec >= 0 ? $"{Mathf.Ceil(durationSec)}" : "");
			}
		}
		return;
	}

	// การ์ด 1 ใบ
	var card = new VBoxContainer
	{
		CustomMinimumSize   = new Vector2(IconSize.X + 4, IconSize.Y + 12),
		SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
		SizeFlagsVertical   = SizeFlags.ShrinkCenter
	};

	Control visual = MakeCrystalVisual(type);
	visual.CustomMinimumSize   = new Vector2(IconSize.X, IconSize.Y);
	visual.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
	visual.SizeFlagsVertical   = SizeFlags.ShrinkCenter;

	var label = new Label
	{
		Text                 = durationSec >= 0 ? $"{Mathf.Ceil(durationSec)}" : "",
		HorizontalAlignment  = HorizontalAlignment.Center,
		VerticalAlignment    = VerticalAlignment.Center,
		SizeFlagsHorizontal  = SizeFlags.ShrinkCenter
	};
	label.AddThemeFontSizeOverride("font_size", 14);

	card.AddChild(visual);
	card.AddChild(label);
	_box.AddChild(card);

	var eNew = new Entry
	{
		Type     = type,
		CardRoot = card,
		Label    = label,
		TimeLeft = durationSec,
		Count    = 0
	};

	// เขียว: x1 เริ่มและไม่มีเวลา
	if (type == CrystalType.Green && durationSec < 0f)
	{
		eNew.Count = 1;
		eNew.Label.Text = "x1";
	}

	// ม่วง: ต้องแสดง "*1 <วินาที>"
	if (type == CrystalType.Purple && durationSec >= 0f)
	{
		eNew.Count      = 1;
		eNew.Label.Text = labelOverride ?? $"*{eNew.Count} {Mathf.Ceil(durationSec)}";
	}

	// แดง: ถ้ามี override (เช่น "-10s" หรือ "+10s") ให้ใช้ตามนั้น
	if (type == CrystalType.Red && !string.IsNullOrEmpty(labelOverride))
	{
		eNew.Label.Text = labelOverride;
	}

	_entries[type] = eNew;
}

	public void ClearBuff(CrystalType type)
	{
		if (!_entries.TryGetValue(type, out var e)) return;

		if (type == CrystalType.Green && e.TimeLeft < 0f)
		{
			e.Count = Math.Max(0, e.Count - 1);
			if (e.Count > 0)
			{
				e.Label.Text = $"x{e.Count}";
				return;
			}
			// ถ้าเหลือ 0 → ค่อยลบการ์ด
		}

		e.CardRoot.QueueFree();
		_entries.Remove(type);
	}

	// ---- helpers ----
	private Control MakeCrystalVisual(CrystalType type)
	{
		var scene = SceneOf(type);
		if (scene == null)
		{
			var l = new Label
			{
				Text = ShortName(type),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			l.AddThemeFontSizeOverride("font_size", 16);
			return l;
		}

		Node inst = scene.Instantiate();
		if (inst is Control ctrl)
		{
			ctrl.MouseFilter = MouseFilterEnum.Ignore;
			var holder = new MarginContainer();
			holder.AddChild(ctrl);
			return holder;
		}
		else
		{
			var vp = new SubViewport
			{
				TransparentBg = true,
				RenderTargetClearMode = SubViewport.ClearMode.Always,
				Size = new Vector2I(IconSize.X, IconSize.Y)
			};
			var root2D = new Node2D();
			vp.AddChild(root2D);
			root2D.AddChild(inst);

			if (inst is Node2D n2d)
			{
				n2d.Scale = new Vector2(Node2DScale, Node2DScale);
				n2d.Position = Vector2.Zero;
			}

			var vc = new SubViewportContainer
			{
				Stretch = true,
				Size = new Vector2(IconSize.X, IconSize.Y)
			};
			vc.AddChild(vp);
			return vc;
		}
	}

	private PackedScene SceneOf(CrystalType t) => t switch
	{
		CrystalType.Blue   => BlueScene,
		CrystalType.Green  => GreenScene,
		CrystalType.Pink   => PinkScene,
		CrystalType.Red    => RedScene,
		CrystalType.Purple => PurpleScene,
		_ => null
	};

	private string ShortName(CrystalType t) => t switch
	{
		CrystalType.Blue   => "B",
		CrystalType.Green  => "G",
		CrystalType.Pink   => "P",
		CrystalType.Red    => "R",
		CrystalType.Purple => "Pu",
		_ => "?"
	};
	public void SetCrystalCount(int count)
{
	if (CrystalCountLabel != null)
		CrystalCountLabel.Text = "x" + count;
}

}
