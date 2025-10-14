using Godot;                                   // อิมพอร์ต namespace ของ Godot (มี Control, Node2D, Label, ฯลฯ)
using System;                                  // อิมพอร์ต System (ใช้ MathF, StringComparison, ฯลฯ)
using System.Linq;                             // อิมพอร์ต LINQ (แม้โค้ดนี้ไม่ได้ใช้หนัก แต่มีไว้สำหรับฟังก์ชันช่วยเกี่ยวกับคอลเลกชัน)
using System.Collections.Generic;              // อิมพอร์ตคอลเลกชันมาตรฐาน (List<T>, Dictionary<,> ฯลฯ)
using GDict = Godot.Collections.Dictionary;    // ตั้งชื่อย่อ GDict ให้กับ Godot.Collections.Dictionary (แบบ dynamic ของ Godot)
using GArray = Godot.Collections.Array;        // ตั้งชื่อย่อ GArray ให้กับ Godot.Collections.Array (อาเรย์แบบ dynamic ของ Godot)

public partial class HighScore : Node2D        // ประกาศคลาส HighScore สืบทอดจาก Node2D (ไว้เป็นซีน/โหนดแสดงสกอร์)
{
	[Export] public NodePath ScrollPath;       // [Export] ให้เซ็ต path ของ ScrollContainer ใน Inspector ได้
	[Export] public NodePath VBoxPath;         // [Export] ให้เซ็ต path ของ VBoxContainer ที่จะวางแถวคะแนน
	[Export] public NodePath BackButtonPath;   // [Export] ให้เซ็ต path ของปุ่มย้อนกลับได้

	private ScrollContainer _scroll;           // ตัวแปรอ้างถึง ScrollContainer (สกรอลล์กรอบรายการ)
	private VBoxContainer   _vbox;             // ตัวแปรอ้างถึง VBoxContainer (คอลัมน์รวมทุกแถว)
	private TextureButton   _backBtn;          // ตัวแปรอ้างถึงปุ่มย้อนกลับ (TextureButton)

	private const float RowH = 28f;            // ค่าคงที่: ความสูงของแต่ละแถวคะแนน (พิกเซล)
	private const int   VisibleRows = 7;       // ค่าคงที่: จำนวนแถวที่อยากให้เห็นใน viewport โดยประมาณ

	public override void _Ready()              // เมธอดเรียกเมื่อโหนดพร้อมทำงาน
	{
		// ❗ ดึงจากซีนเท่านั้น (ไม่ new เพื่อลดโอกาส handle ไม่พร้อม)
		_scroll  = GetNodeOrNull<ScrollContainer>(ScrollPath);   // หา ScrollContainer จาก path ที่กำหนด (อาจเป็น null ได้)
		_vbox    = GetNodeOrNull<VBoxContainer>(VBoxPath);       // หา VBoxContainer จาก path (อาจเป็น null ได้)
		_backBtn = GetNodeOrNull<TextureButton>(BackButtonPath); // หา TextureButton จาก path (อาจเป็น null ได้)

		if (_scroll == null || _vbox == null)                    // ถ้าโหนดหลัก ๆ ไม่พบ
		{
			GD.PushError("[HighScore] Please assign ScrollPath & VBoxPath in Inspector."); // แจ้ง error ใน Output
			return;                                              // ยุติการทำงานต่อ เพื่อไม่ให้ NullReference ในภายหลัง
		}

		_scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled; // ปิดสกรอลแนวนอน
		_scroll.VerticalScrollMode   = ScrollContainer.ScrollMode.Auto;     // เปิดสกรอลแนวตั้งแบบอัตโนมัติ
		_scroll.ClipContents         = true;                                 // ตัดส่วนที่เกินขนาดคอนเทนเนอร์ (ไม่ให้ล้น)
		_scroll.CustomMinimumSize    = new Vector2(0, RowH * VisibleRows + 4); // กำหนดความสูงขั้นต่ำตามจำนวนแถวที่อยากเห็น
		_scroll.SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill;         // ให้ยืดเต็มแนวนอนใน parent
		_scroll.SizeFlagsVertical    = Control.SizeFlags.ShrinkCenter;       // จัดตำแหน่งแนวตั้งให้อยู่กลางถ้าไม่เต็ม

		_vbox.Alignment = BoxContainer.AlignmentMode.Begin;                   // จัดลูกของ VBox ให้ชิดบน
		_vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;            // ให้ VBox ยืดเต็มแนวนอน
		_vbox.AddThemeConstantOverride("separation", 2);                     // กำหนดช่องว่างระหว่างลูกของ VBox = 2px

		if (_backBtn != null) _backBtn.Pressed += OnBackPressed;             // ถ้ามีปุ่มย้อนกลับ: ผูกอีเวนต์กดปุ่มให้เรียก OnBackPressed

		Populate();                                                          // สร้าง/เติมรายการสกอร์ลงใน VBox
	}

	private void Populate()                                                  // เมธอดสร้าง UI ของรายการ High Score ทั้งหมด
	{
		foreach (Node c in _vbox.GetChildren()) c.QueueFree();               // ล้างลูกเดิมใน VBox (กันซ้ำ)

		var doc = LeaderboardStore.LoadDoc();                                // โหลดเอกสาร/ข้อมูลลีดเดอร์บอร์ดจากที่เก็บ (เช่นไฟล์)
		if (!doc.ContainsKey("leaderboards"))                                // ถ้าไม่มีคีย์ "leaderboards" แปลว่ายังไม่เคยมีข้อมูล
		{
			_vbox.AddChild(MakeLabel("ยังไม่มีข้อมูลอันดับ"));               // ใส่ข้อความแจ้งผู้ใช้
			return;                                                           // จบการทำงาน
		}

		var lbs = (GDict)doc["leaderboards"];                                // อ้างอิงอ็อบเจ็กต์ลีดเดอร์บอร์ด (เป็น Dictionary)
		
		string currentUser = PlayerLogin.Instance?.CurrentUser?.PlayerName ?? "";

		// วันที่ (ใหม่ → เก่า)
		var dateKeys = new List<string>();                                   // เตรียมลิสต์เก็บคีย์วันที่ (string)
		foreach (var k in lbs.Keys) dateKeys.Add(k.AsString());              // ดึงทุกคีย์มาเป็น string แล้วใส่ลิสต์
		dateKeys.Sort((a, b) => string.Compare(b, a, StringComparison.Ordinal)); // เรียงจากใหม่ไปเก่า (b,a)

		foreach (var dateKey in dateKeys)                                    // วนตามวัน (ใหม่ → เก่า)
		{
			var dateNode = (GDict)lbs[dateKey];                               // ดึง node ของวันนั้น (เก็บกลุ่มตามเลเวล)
			_vbox.AddChild(MakeHeader($"{LeaderboardStore.FormatThaiDateForHeader(dateKey)} ")); // เพิ่มหัวข้อวัน (จัดรูปแบบไทย)

			// เลเวล (1 → มาก)
			var levelKeys = new List<int>();                                  // เตรียมลิสต์เก็บเลเวลเป็น int
			foreach (var lk in dateNode.Keys)                                 // วนคีย์ทุกเลเวลของวันนั้น
				if (int.TryParse(lk.AsString(), out var idx)) levelKeys.Add(idx); // แปลงเป็น int ได้ก็เก็บ
			levelKeys.Sort();                                                 // เรียงจากเล็กไปใหญ่ (เลเวลต่ำไปสูง)

			// ใช้ for(i) เพื่อรู้ว่ากลุ่มสุดท้ายหรือยัง
			for (int i = 0; i < levelKeys.Count; i++)                         // วนตามเลเวล พร้อมรู้ index ปัจจุบัน
			{
				int lev = levelKeys[i];                                       // ค่าเลเวลปัจจุบัน
				var arr = (GArray)dateNode[lev.ToString()];                   // ดึงรายการคะแนนของเลเวลนี้ (เป็นอาร์เรย์ของแถว)
				if (arr.Count == 0) continue;                                 // ถ้าไม่มีข้อมูล ข้าม

				int rank = 1;                                                 // เริ่มลำดับอันดับที่ 1
				foreach (GDict row in arr)                                    // วนทุกแถวในเลเวลนี้
				{
					string name  = row.ContainsKey("name")  ? row["name"].AsString() : "-"; // ชื่อผู้เล่น (ไม่มีให้เป็น "-")
					int score    = row.ContainsKey("score") ? (int)(long)row["score"] : 0;  // คะแนน (แปลงจาก long → int)

					// แถวหลัก
					var rowBox = new HBoxContainer { CustomMinimumSize = new Vector2(0, RowH) }; // สร้างกล่องแนวนอน เป็นแถวหนึ่ง
					rowBox.AddThemeConstantOverride("separation", 4);                            // ระยะห่างระหว่างคอลัมน์ในแถว
					
					var nameCell = MakeCell($"{rank}. {name}", expand: false, alignRight: false);

					// 💙 ถ้าเป็นชื่อผู้เล่นปัจจุบัน → เปลี่ยนสีฟ้า
					if (name == currentUser)
					nameCell.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 1f));

					// ซ้าย: ชื่อ (ไม่ยืด) → ให้กลุ่มขวาเข้ามาใกล้ชื่อ
					rowBox.AddChild(nameCell); // ช่องชื่อ + อันดับ ไม่ยืด

					// ขวา: กลุ่ม [level][score] วางต่อจากชื่อ
					var rightGroup = new HBoxContainer();                                         // กล่องย่อยทางขวา
					rightGroup.AddThemeConstantOverride("separation", 100);                       // เว้นระยะห่างใหญ่ระหว่าง "level" กับ "score"
					rightGroup.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;               // จัดให้เริ่มชิดซ้ายของกลุ่ม

					rightGroup.AddChild(MakeFixedCell($"level {lev}", minW: 70, alignRight: false)); // ช่องแสดงเลเวล กว้างขั้นต่ำ 70
					rightGroup.AddChild(MakeFixedCell($"{score}",     minW: 64, alignRight: true));  // ช่องคะแนน กว้างขั้นต่ำ 64 ชิดขวา

					rowBox.AddChild(rightGroup);                                                   // ใส่กลุ่มขวาลงในแถว

					// ตัวคั่นยืดได้ด้านท้าย
					rowBox.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }); // ตัวดันให้เนื้อหาซ้ายติดกัน

					_vbox.AddChild(rowBox);                                                        // เพิ่มแถวนี้ลงใน VBox
					rank++;                                                                        // อันดับถัดไป
				}

				// ── คั่นกลุ่มเลเวลด้วย "เส้นประ" ถ้ายังไม่ใช่กลุ่มสุดท้าย ──
				bool notLastGroup = (i < levelKeys.Count - 1);                                     // เช็คว่าเลเวลนี้ยังไม่ใช่อันสุดท้ายไหม
				if (notLastGroup)
				{
					_vbox.AddChild(MakeDashedSeparator(height: 2, alpha: 0.22f, dash: 10f, gap: 6f)); // เพิ่มเส้นประคั่นกลุ่ม
					_vbox.AddChild(MakeSpacer(6));                                                  // เพิ่มช่องว่างเล็กน้อยหลังเส้นประ
				}
				else
				{
					_vbox.AddChild(MakeSpacer(6));                                                  // ถ้าเป็นกลุ่มสุดท้ายของวัน ก็ใส่ช่องว่างเฉย ๆ
				}
			}

			// เส้นทึบปิดท้ายแต่ละวัน
			_vbox.AddChild(MakeDivider());                                                         // ใส่เส้นทึบคั่นระหว่าง “วัน” ถัดไป
		}
	}

	// ===== UI helpers =====
	private Control MakeHeader(string text)                                                        // สร้าง Label หัวข้อวัน (ขนาดใหญ่ สีเหลือง)
	{
		var lbl = new Label { Text = text };                                                       // สร้าง Label พร้อมข้อความ
		lbl.AddThemeFontSizeOverride("font_size", 24);                                             // ขนาดฟอนต์ 24
		lbl.AddThemeColorOverride("font_color", Colors.Yellow);                                    // สีเหลือง
		lbl.AddThemeConstantOverride("margin_left", 6);                                            // ขยับเข้าไปทางขวานิดหน่อย
		return lbl;                                                                                // ส่งกลับ Label
	}

	private Control MakeDivider()                                                                  // สร้างเส้นทึบคั่น “วัน”
	{
		var c = new ColorRect { Color = new Color(1, 1, 1, 0.1f) };                                // สร้างสี่เหลี่ยมสีขาวโปร่ง (10%)
		c.CustomMinimumSize = new Vector2(0, 2);                                                   // ความสูง 2px
		c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;                                      // ยืดเต็มแนวนอน
		return c;                                                                                  // ส่งกลับคอนโทรล
	}

	private Control MakeSpacer(int h)                                                              // สร้างช่องว่างแนวตั้งสูง h พิกเซล
	{
		var c = new Control();                                                                      // คอนโทรลเปล่า
		c.CustomMinimumSize = new Vector2(0, h);                                                    // ตั้งความสูงขั้นต่ำ
		return c;                                                                                   // ส่งกลับ
	}

	private Control MakeLabel(string text)                                                         // สร้าง Label ข้อความทั่วไป (เช่น “ยังไม่มีข้อมูลอันดับ”)
	{
		var lbl = new Label { Text = text };                                                       // สร้าง Label
		lbl.AddThemeFontSizeOverride("font_size", 22);                                             // ฟอนต์ 22
		lbl.AddThemeColorOverride("font_color", Colors.White);                                     // สีขาว
		lbl.AddThemeConstantOverride("margin_left", 6);                                            // เว้นซ้ายเล็กน้อย
		return lbl;                                                                                // ส่งกลับ
	}

	private Control MakeCell(string text, bool expand, bool alignRight)                            // สร้างเซลล์ Label แบบยืด/ไม่ยืด และเลือกชิดซ้าย/ขวาได้
	{
		var lbl = new Label { Text = text, ClipText = true };                                      // Label โดยเปิด ClipText กันข้อความล้น
		lbl.HorizontalAlignment = alignRight ? HorizontalAlignment.Right : HorizontalAlignment.Left; // จัดแนวนอนซ้าย/ขวา
		lbl.AddThemeFontSizeOverride("font_size", 22);                                             // ฟอนต์ 22
		lbl.AddThemeColorOverride("font_color", Colors.White);                                     // สีขาว
		lbl.AddThemeConstantOverride("margin_left", 0);                                            // ไม่เพิ่ม margin ซ้าย
		lbl.CustomMinimumSize = new Vector2(42 * 5, RowH);                                         // กำหนด min size (กว้างคร่าว ๆ 210px, สูง RowH)
		lbl.SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill                            // ถ้า expand = true ให้ยืดเต็มแนวนอน
										 : Control.SizeFlags.ShrinkBegin;                         // ถ้าไม่ ให้ชิดซ้ายและไม่ยืด
		return lbl;                                                                                // ส่งกลับ
	}

	private Control MakeFixedCell(string text, int minW, bool alignRight)                          // สร้างเซลล์ Label ความกว้างขั้นต่ำคงที่
	{
		var lbl = new Label { Text = text, ClipText = true };                                      // Label พร้อม ClipText
		lbl.HorizontalAlignment = alignRight ? HorizontalAlignment.Right : HorizontalAlignment.Left; // ชิดขวา/ซ้าย
		lbl.AddThemeFontSizeOverride("font_size", 22);                                             // ฟอนต์ 22
		lbl.AddThemeColorOverride("font_color", Colors.White);                                     // สีขาว
		lbl.AddThemeConstantOverride("margin_left", 0);                                            // ไม่เพิ่ม margin ซ้าย
		lbl.CustomMinimumSize = new Vector2(minW, RowH);                                           // กำหนดความกว้างขั้นต่ำตาม minW และสูง RowH
		lbl.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;                                     // จัดให้ชิดขวาของกล่องที่ครอบ
		return lbl;                                                                                // ส่งกลับ
	}

	// ★ helper สร้างเส้นประ
	private Control MakeDashedSeparator(int height = 2, float alpha = 0.22f, float dash = 10f, float gap = 6f) // ฟังก์ชันสร้างตัวคั่นแบบเส้นประ
	{
		return new DashedSeparator                                                                  // สร้างอินสแตนซ์ของคอมโพเนนต์เส้นประ (คลาสที่คุณทำไว้)
		{
			Thickness = height,                                                                     // ตั้งความหนาเส้น
			Dash = dash,                                                                            // ความยาวช่วงเส้น
			Gap = gap,                                                                              // ความยาวช่องว่าง
			LineColor = new Color(1, 1, 1, alpha),                                                  // สีเส้น (ขาวโปร่งตาม alpha)
			CustomMinimumSize = new Vector2(0, height + 2),                                         // ความสูงขั้นต่ำ (บวก 2 เผื่อ)
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill                                      // ยืดเต็มแนวนอน
		};
	}

	private void OnBackPressed()                                                                    // เมธอดเรียกเมื่อกดปุ่มย้อนกลับ
	{
		GetTree().ChangeSceneToFile("res://SceneStartandHigh/StartGame.tscn");                      // สลับซีนไปหน้า StartGame
	}
}
