using Godot;
using System;
using System.Globalization;
using System.Collections.Generic;

public partial class HighScore : Node2D
{
	// ชี้ node ได้จาก Inspector; ถ้าไม่ตั้งจะพยายามหา/สร้างให้เอง
	[Export] public NodePath ScrollPath;
	[Export] public NodePath VBoxPath;
	[Export] public NodePath BackButtonPath;

	private ScrollContainer _scroll;
	private VBoxContainer   _vbox;
	private TextureButton   _backBtn;

	private const float RowHeight   = 28f;  // ความสูงต่อบรรทัด
	private const int   VisibleRows = 7;    // ✅ โชว์เริ่มต้น 7 ชื่อ

	public override void _Ready()
	{
		// หา ScrollContainer (หรือสร้างใหม่ถ้าไม่มี)
		_scroll = GetNodeOrNull<ScrollContainer>(ScrollPath)
			   ?? GetNodeOrNull<ScrollContainer>("ScrollContainer");

		if (_scroll == null)
		{
			_scroll = new ScrollContainer { Name = "ScrollContainer" };
			AddChild(_scroll);
		}

		// ตั้งโหมดเลื่อน
		_scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		_scroll.VerticalScrollMode   = ScrollContainer.ScrollMode.Auto;
		_scroll.ClipContents         = true;

		// จำกัดพื้นที่แสดงผลให้เท่ากับ 7 แถว
		((Control)_scroll).CustomMinimumSize = new Vector2(0, RowHeight * VisibleRows + 4);
		_scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_scroll.SizeFlagsVertical   = Control.SizeFlags.ShrinkCenter; // ไม่ดึงให้สูงเกินจำเป็น

		// หา/สร้าง VBoxContainer ภายใน ScrollContainer
		_vbox = GetNodeOrNull<VBoxContainer>(VBoxPath)
			 ?? _scroll.GetNodeOrNull<VBoxContainer>("VBoxContainer");

		if (_vbox == null)
		{
			_vbox = new VBoxContainer { Name = "VBoxContainer" };
			_scroll.AddChild(_vbox);
		}

		// ให้เรียงจากบนลงล่าง ชิดซ้าย และกินแนวนอนเต็ม
		_vbox.Alignment = BoxContainer.AlignmentMode.Begin;
		_vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		// ปุ่มกลับ
		_backBtn = GetNodeOrNull<TextureButton>(BackButtonPath)
				?? GetNodeOrNull<TextureButton>("Sprite2D/BackButton");
		if (_backBtn != null)
			_backBtn.Pressed += OnBackPressed;

		PopulateList();
	}

	private void PopulateList()
	{
		foreach (Node c in _vbox.GetChildren()) c.QueueFree();

		var players = PlayerLogin.Instance?.LoadPlayers();
		if (players == null || players.Count == 0)
		{
			_vbox.AddChild(MakeRow("No players yet"));
			return;
		}

		// ใส่ “ทุกรายชื่อ” ลงไป → ส่วนแสดงผลสูงเท่า 7 แถวจะทำให้ต้องเลื่อนเพื่อดูที่เหลือ
		foreach (var p in players)
		{
			string dateText = p.CreatedAt;
			if (DateTime.TryParse(p.CreatedAt, out var dt))
				dateText = dt.ToLocalTime().ToString("dd/MM/yyyy", new CultureInfo("th-TH"));

			_vbox.AddChild(MakeRow($"{p.PlayerName}  {dateText}"));
		}
	}

	private Label MakeRow(string text)
	{
		var lbl = new Label { Text = text, ClipText = true };
		lbl.HorizontalAlignment = HorizontalAlignment.Left;
		lbl.AddThemeColorOverride("font_color", Colors.White);
		lbl.AddThemeFontSizeOverride("font_size", 22);
		lbl.CustomMinimumSize = new Vector2(0, RowHeight);
		lbl.AddThemeConstantOverride("margin_left", 20);
		return lbl;
	}

	private void OnBackPressed()
	{
		GetTree().ChangeSceneToFile("res://SceneStartandHigh/StartGame.tscn");
	}
}
