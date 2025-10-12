using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
// Fix ambiguity
using GDict = Godot.Collections.Dictionary;
using GArray = Godot.Collections.Array;

public partial class HighScore : Node2D
{
	[Export] public NodePath ScrollPath;
	[Export] public NodePath VBoxPath;
	[Export] public NodePath BackButtonPath;

	private ScrollContainer _scroll;
	private VBoxContainer   _vbox;
	private TextureButton   _backBtn;

	private const float RowH = 28f;
	private const int   VisibleRows = 7;

	public override void _Ready()
	{
		_scroll = GetNodeOrNull<ScrollContainer>(ScrollPath)
			   ?? GetNodeOrNull<ScrollContainer>("ScrollContainer")
			   ?? new ScrollContainer { Name = "ScrollContainer" };
		if (_scroll.GetParent() == null) AddChild(_scroll);

		_scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		_scroll.VerticalScrollMode   = ScrollContainer.ScrollMode.Auto;
		_scroll.ClipContents         = true;
		((Control)_scroll).CustomMinimumSize = new Vector2(0, RowH * VisibleRows + 4);
		_scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_scroll.SizeFlagsVertical   = Control.SizeFlags.ShrinkCenter;

		_vbox = GetNodeOrNull<VBoxContainer>(VBoxPath)
			 ?? _scroll.GetNodeOrNull<VBoxContainer>("VBoxContainer")
			 ?? new VBoxContainer { Name = "VBoxContainer" };
		if (_vbox.GetParent() == null) _scroll.AddChild(_vbox);
		_vbox.Alignment = BoxContainer.AlignmentMode.Begin;
		_vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		_backBtn = GetNodeOrNull<TextureButton>(BackButtonPath)
				?? GetNodeOrNull<TextureButton>("Sprite2D/BackButton");
		if (_backBtn != null) _backBtn.Pressed += OnBackPressed;

		Populate();
	}

	private void Populate()
	{
		foreach (Node c in _vbox.GetChildren()) c.QueueFree();

		var doc = LeaderboardStore.LoadDoc();
		if (!doc.ContainsKey("leaderboards"))
		{
			_vbox.AddChild(MakeLabel("ยังไม่มีข้อมูลอันดับ"));
			return;
		}

		var lbs = (GDict)doc["leaderboards"];

		// วันที่ (ใหม่ → เก่า)
		var dateKeys = new List<string>();
		foreach (var k in lbs.Keys) dateKeys.Add(k.AsString());
		dateKeys.Sort((a, b) => string.Compare(b, a, StringComparison.Ordinal));

		foreach (var dateKey in dateKeys)
		{
			var dateNode = (GDict)lbs[dateKey];

			_vbox.AddChild(MakeHeader($"{LeaderboardStore.FormatThaiDateForHeader(dateKey)} "));

			// เลเวล (1 → มาก)
			var levelKeys = new List<int>();
			foreach (var lk in dateNode.Keys)
				if (int.TryParse(lk.AsString(), out var idx)) levelKeys.Add(idx);
			levelKeys.Sort();

			foreach (var lev in levelKeys)
			{
				var arr = (GArray)dateNode[lev.ToString()];
				if (arr.Count == 0) continue;

				int rank = 1;
				foreach (GDict row in arr)
				{
					string name = row.ContainsKey("name") ? row["name"].AsString() : "-";
					int score   = row.ContainsKey("score") ? (int)(long)row["score"] : 0;

					var h = new HBoxContainer { CustomMinimumSize = new Vector2(0, RowH) };
					h.AddThemeConstantOverride("separation", 4); // ลดช่องว่างระหว่างคอลัมน์ให้ชิดขึ้น

					// ชื่อ 4 | เลเวล 3 | คะแนน (ไม่ขยาย, ชิดกับเลเวล)
					h.AddChild(MakeCell($"{rank}. {name}", 4));
					h.AddChild(MakeCell($"level {lev}",    3));
					h.AddChild(MakeScoreCell($"{score}"));   // << คอลัมน์คะแนนแบบพิเศษ
					_vbox.AddChild(h);
					rank++;
				}
				_vbox.AddChild(MakeSpacer(6));
			}
			_vbox.AddChild(MakeDivider());
		}
	}

	// ===== UI helpers =====
	private Control MakeHeader(string text)
	{
		var lbl = new Label { Text = text };
		lbl.AddThemeFontSizeOverride("font_size", 24);
		lbl.AddThemeColorOverride("font_color", Colors.Yellow);
		lbl.AddThemeConstantOverride("margin_left", 6);
		return lbl;
	}

	private Control MakeDivider()
	{
		var c = new ColorRect { Color = new Color(1, 1, 1, 0.1f) };
		c.CustomMinimumSize = new Vector2(0, 2);
		c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		return c;
	}

	private Control MakeSpacer(int h)
	{
		var c = new Control();
		c.CustomMinimumSize = new Vector2(0, h);
		return c;
	}

	private Control MakeLabel(string text)
	{
		var lbl = new Label { Text = text };
		lbl.AddThemeFontSizeOverride("font_size", 22);
		lbl.AddThemeColorOverride("font_color", Colors.White);
		lbl.AddThemeConstantOverride("margin_left", 6);
		return lbl;
	}

	private Control MakeCell(string text, int weight = 1, bool alignRight = false)
	{
		var lbl = new Label { Text = text, ClipText = true };
		lbl.HorizontalAlignment = alignRight ? HorizontalAlignment.Right : HorizontalAlignment.Left;
		lbl.AddThemeFontSizeOverride("font_size", 22);
		lbl.AddThemeColorOverride("font_color", Colors.White);
		lbl.AddThemeConstantOverride("margin_left", 6);
		lbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		// ทำให้คอลัมน์ไม่กว้างเกินไป (ยืดได้ แต่ฐานแคบลง)
		lbl.CustomMinimumSize = new Vector2(42 * weight, RowH);
		return lbl;
	}

	// คอลัมน์คะแนน: ให้แคบ, ชิดขวา, และ "ไม่" ExpandFill → จะเกาะใกล้คอลัมน์เลเวล
	private Control MakeScoreCell(string text)
	{
		var lbl = new Label { Text = text, ClipText = true };
		lbl.HorizontalAlignment = HorizontalAlignment.Right;
		lbl.AddThemeFontSizeOverride("font_size", 22);
		lbl.AddThemeColorOverride("font_color", Colors.White);
		lbl.AddThemeConstantOverride("margin_left", 2);
		lbl.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd; // ไม่ขยาย
		lbl.CustomMinimumSize = new Vector2(36, RowH);          // ความกว้างพอดีตัวเลข 3–4 หลัก
		return lbl;
	}

	private void OnBackPressed()
	{
		GetTree().ChangeSceneToFile("res://SceneStartandHigh/StartGame.tscn");
	}
}
