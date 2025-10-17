using Godot;                                  
using System;                                 
using System.Linq;                             // อิมพอร์ต LINQ ต่มีไว้สำหรับฟังก์ชันช่วยเกี่ยวกับคอลเลกชัน
using System.Collections.Generic;              // อิมพอร์ตคอลเลกชันมาตรฐาน (List<T>, Dictionary<,> ฯลฯ)
using GDict = Godot.Collections.Dictionary;    // ตั้งชื่อย่อ GDict ให้กับ Godot.Collections.Dictionary (แบบ dynamic ของ Godot)
using GArray = Godot.Collections.Array;        // ตั้งชื่อย่อ GArray ให้กับ Godot.Collections.Array (อาเรย์แบบ dynamic ของ Godot)

// ประกาศคลาส HighScore สืบทอดจาก Node2D 
public partial class HighScore : Node2D        
{
	[Export] public NodePath ScrollPath; // [Export] ให้เซ็ต path ของ ScrollContainer ใน Inspector ได้
	[Export] public NodePath VBoxPath;// [Export] ให้เซ็ต path ของ VBoxContainer ที่จะวางแถวคะแนน
	[Export] public NodePath BackButtonPath;// [Export] ให้เซ็ต path ของปุ่มย้อนกลับได้

	private ScrollContainer _scroll;  // ตัวแปรอ้างถึง ScrollContainer (สกรอลล์กรอบรายการ)
	private VBoxContainer   _vbox; // ตัวแปรอ้างถึง VBoxContainer (คอลัมน์รวมทุกแถว)
	private TextureButton   _backBtn;  // ตัวแปรอ้างถึงปุ่มย้อนกลับ (TextureButton)

	private const float RowH = 28f;  // ความสูงของแต่ละแถวคะแนน 
	private const int   VisibleRows = 7; // จำนวนแถวที่อยากให้เห็นใน viewport โดยประมาณ
	
	// เมธอดเรียกเมื่อโหนดพร้อมทำงาน
	public override void _Ready() 
	{
		// ดึงจากซีนเท่านั้น 
		_scroll  = GetNodeOrNull<ScrollContainer>(ScrollPath);// หา ScrollContainer จาก path ที่กำหนด (อาจเป็น null ได้)
		_vbox    = GetNodeOrNull<VBoxContainer>(VBoxPath);  // หา VBoxContainer จาก path (อาจเป็น null ได้)
		_backBtn = GetNodeOrNull<TextureButton>(BackButtonPath); // หา TextureButton จาก path (อาจเป็น null ได้)
		
		 // ถ้าโหนดหลัก ๆ ไม่พบ
		if (_scroll == null || _vbox == null) 
		{
			// แจ้ง error ใน Output
			GD.PushError("[HighScore] Please assign ScrollPath & VBoxPath in Inspector."); 
			return;   // ยุติการทำงานต่อ เพื่อไม่ให้ NullReference ในภายหลัง
		}

		_scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled; // ปิดสกรอลแนวนอน
		_scroll.VerticalScrollMode   = ScrollContainer.ScrollMode.Auto;  // เปิดสกรอลแนวตั้งแบบอัตโนมัติ
		_scroll.ClipContents         = true;   // ตัดส่วนที่เกินขนาดคอนเทนเนอร์ (ไม่ให้ล้น)
		_scroll.CustomMinimumSize    = new Vector2(0, RowH * VisibleRows + 4); // กำหนดความสูงขั้นต่ำตามจำนวนแถวที่อยากเห็น
		_scroll.SizeFlagsHorizontal  = Control.SizeFlags.ExpandFill;   // ให้ยืดเต็มแนวนอนใน parent
		_scroll.SizeFlagsVertical    = Control.SizeFlags.ShrinkCenter; // จัดตำแหน่งแนวตั้งให้อยู่กลางถ้าไม่เต็ม

		_vbox.Alignment = BoxContainer.AlignmentMode.Begin;  // จัดลูกของ VBox ให้ชิดบน
		_vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;  // ให้ VBox ยืดเต็มแนวนอน
		_vbox.AddThemeConstantOverride("separation", 2);  // กำหนดช่องว่างระหว่างลูกของ VBox = 2px
		
		// ถ้ามีปุ่มย้อนกลับ: ผูกอีเวนต์กดปุ่มให้เรียก OnBackPressed
		if (_backBtn != null) _backBtn.Pressed += OnBackPressed; 
		// สร้าง/เติมรายการสกอร์ลงใน VBox
		Populate();  
	}
	
	// เมธอดสร้าง UI ของรายการ High Score ทั้งหมด
	private void Populate()                                                  
	{
		// ล้างลูกเดิมใน VBox (กันซ้ำ)
		foreach (Node c in _vbox.GetChildren()) c.QueueFree(); 
		// โหลดเอกสาร/ข้อมูลลีดเดอร์บอร์ดจากที่เก็บ (เช่นไฟล์)
		var doc = LeaderboardStore.LoadDoc(); 
		if (!doc.ContainsKey("leaderboards"))  // ถ้าไม่มีคีย์ "leaderboards" แปลว่ายังไม่เคยมีข้อมูล
		{
			_vbox.AddChild(MakeLabel("ยังไม่มีข้อมูลอันดับ"));  
			return;  
		}
		// อ้างอิงอ็อบเจ็กต์ลีดเดอร์บอร์ด เป็น Dictionary
		var lbs = (GDict)doc["leaderboards"];  
		
		//สร้างตัวแปร currentUser ขึ้นมา ถ้า PlayerLogin.Instance และ CurrentUser มีอยู่ → เอา PlayerName
		//ถ้าไม่มี (null) → ให้เป็นสตริงว่าง "" แทน
		string currentUser = PlayerLogin.Instance?.CurrentUser?.PlayerName ?? "";

		// วันที่ (ใหม่ → เก่า)
		var dateKeys = new List<string>();  // เตรียมลิสต์เก็บคีย์วันที่ (string)
		foreach (var k in lbs.Keys) dateKeys.Add(k.AsString()); // ดึงทุกคีย์มาเป็น string แล้วใส่ลิสต์
		dateKeys.Sort((a, b) => string.Compare(b, a, StringComparison.Ordinal)); // เรียงจากใหม่ไปเก่า (b,a)
		
		// วนตามวัน (ใหม่ ไป เก่า)
		foreach (var dateKey in dateKeys) 
		{
			var dateNode = (GDict)lbs[dateKey]; // ดึง node ของวันนั้น (เก็บกลุ่มตามเลเวล)
			_vbox.AddChild(MakeHeader($"{LeaderboardStore.FormatThaiDateForHeader(dateKey)} ")); // เพิ่มหัวข้อวัน (จัดรูปแบบไทย)

			// เลเวล (1 → มาก)
			var levelKeys = new List<int>();  // เตรียมลิสต์เก็บเลเวลเป็น int
			foreach (var lk in dateNode.Keys)   // วนคีย์ทุกเลเวลของวันนั้น
				if (int.TryParse(lk.AsString(), out var idx)) levelKeys.Add(idx); // แปลงเป็น int ได้ก็เก็บ
			levelKeys.Sort();  // เรียงจากเล็กไปใหญ่ (เลเวลต่ำไปสูง)

			// ใช้ for(i) เพื่อรู้ว่ากลุ่มสุดท้ายหรือยัง
			for (int i = 0; i < levelKeys.Count; i++)  // วนตามเลเวล พร้อมรู้ index ปัจจุบัน
			{
				int lev = levelKeys[i];// ค่าเลเวลปัจจุบัน
				var arr = (GArray)dateNode[lev.ToString()]; // ดึงรายการคะแนนของเลเวลนี้ (เป็นอาร์เรย์ของแถว)
				if (arr.Count == 0) continue; // ถ้าไม่มีข้อมูล ข้าม

				int rank = 1;  // เริ่มลำดับอันดับที่ 1
				foreach (GDict row in arr)   // วนทุกแถวในเลเวลนี้
				{
					string name  = row.ContainsKey("name")  ? row["name"].AsString() : "-"; // ชื่อผู้เล่น (ไม่มีให้เป็น "-")
					int score    = row.ContainsKey("score") ? (int)(long)row["score"] : 0;  // คะแนน (แปลงจาก long → int)

					// แถวหลัก
					// สร้างกล่องแนวนอน เป็นแถวหนึ่ง
					var rowBox = new HBoxContainer { CustomMinimumSize = new Vector2(0, RowH) };
					rowBox.AddThemeConstantOverride("separation", 4);  // ระยะห่างระหว่างคอลัมน์ในแถว
					
					var nameCell  = MakeCell($"{rank}. {name}", expand: false, alignRight: false); // cell แสดง "ลำดับ. ชื่อ" เช่น "1. Nach" (ชิดซ้าย)
					var levelCell = MakeFixedCell($"level {lev}", minW: 70, alignRight: false);  // cell แสดงเลเวล เช่น "level 5" (กว้างขั้นต่ำ 70px)
					var scoreCell = MakeFixedCell($"{score}", minW: 64, alignRight: true);  // cell แสดงคะแนน เช่น "12400" (ชิดขวาให้ตัวเลขเรียง)

					// ถ้าเป็นชื่อผู้เล่นปัจจุบัน → เปลี่ยนสีฟ้า
					if (name == currentUser)
					nameCell.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 1f));

					// ซ้าย: ชื่อ (ไม่ยืด) → ให้กลุ่มขวาเข้ามาใกล้ชื่อ
					rowBox.AddChild(nameCell); // ช่องชื่อ + อันดับ ไม่ยืด

					// ขวา: กลุ่ม [level][score] วางต่อจากชื่อ
					// ขวา: กลุ่ม [level][score] วางต่อจากชื่อ
					var rightGroup = new HBoxContainer();  // กล่องย่อยทางขวา
					rightGroup.AddThemeConstantOverride("separation", 100);// เว้นระยะห่างใหญ่ระหว่าง "level" กับ "score"
					rightGroup.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin; // จัดให้เริ่มชิดซ้ายของกลุ่ม

					// ใช้ชื่อใหม่เพื่อไม่ชน
					var myLevelCell = MakeFixedCell($"level {lev}", minW: 70, alignRight: false);
					var myScoreCell = MakeFixedCell($"{score}", minW: 64, alignRight: true);

					// ถ้าเป็นชื่อผู้เล่นที่ล็อกอินอยู่ ให้เปลี่ยนสีตัวอักษรเป็นฟ้า
					if (name == currentUser) // ตรวจชื่อในลิสต์ตรงกับผู้เล่นปัจจุบันไหม
					{
						var blue = new Color(0.4f, 0.8f, 1f);  // สร้างค่าสีฟ้าอ่อน (RGB 0.4, 0.8, 1)
						myLevelCell.AddThemeColorOverride("font_color", blue); // เปลี่ยนสีฟอนต์ของช่องเลเวล
						myScoreCell.AddThemeColorOverride("font_color", blue); // เปลี่ยนสีฟอนต์ของช่องคะแนน
					}

					// เพิ่มช่องแสดงเลเวลและคะแนนเข้า group ด้านขวา
					rightGroup.AddChild(myLevelCell);  // เพิ่มช่องเลเวล
					rightGroup.AddChild(myScoreCell);  // เพิ่มช่องคะแนน

					rowBox.AddChild(rightGroup);  // ใส่กลุ่มขวาลงในแถว
					rowBox.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }); // ตัวดันให้เนื้อหาซ้ายติดกัน
					
					// เพิ่มกล่องแถว (rowBox) ของผู้เล่นลงใน VBox รวมทั้งหมด เพื่อให้แสดงเรียงในแนวตั้ง
					_vbox.AddChild(rowBox);  
					rank++;  // เพิ่มค่าลำดับ (rank) ขึ้น 1 สำหรับผู้เล่นถัดไปในตาราง
				}

				// คั่นกลุ่มเลเวลด้วย "เส้นประ" ถ้ายังไม่ใช่กลุ่มสุดท้าย 
				bool notLastGroup = (i < levelKeys.Count - 1); // เช็คว่าเลเวลนี้ยังไม่ใช่อันสุดท้ายไหม
					if (notLastGroup)
					{
						// เพิ่มเส้นประคั่นกลุ่ม
						_vbox.AddChild(MakeDashedSeparator(height: 2, alpha: 0.22f, dash: 10f, gap: 6f)); 
						_vbox.AddChild(MakeSpacer(6));  // เพิ่มช่องว่างเล็กน้อยหลังเส้นประ
					}
					else
					{
						 // ถ้าเป็นกลุ่มสุดท้ายของวัน ก็ใส่ช่องว่างเฉย ๆ
						_vbox.AddChild(MakeSpacer(6));  
					}
			}
				// เส้นทึบปิดท้ายแต่ละวัน
			_vbox.AddChild(MakeDivider());  
		}
	}
	// UI helpers
	// สร้าง Label หัวข้อวัน (ขนาดใหญ่ สีเหลือง)
	private Control MakeHeader(string text)   
	{
		var lbl = new Label { Text = text };  // สร้าง Label พร้อมข้อความ
		lbl.AddThemeFontSizeOverride("font_size", 24);   // ขนาดฟอนต์ 24
		lbl.AddThemeColorOverride("font_color", Colors.Yellow); // สีเหลือง
		lbl.AddThemeConstantOverride("margin_left", 6);  // ขยับเข้าไปทางขวานิดหน่อย
		return lbl;  // ส่งกลับ Label
	}
	// สร้างเส้นทึบคั่น “วัน”
	private Control MakeDivider()  
	{
		var c = new ColorRect { Color = new Color(1, 1, 1, 0.1f) };  // สร้างสี่เหลี่ยมสีขาวโปร่ง (10%)
		c.CustomMinimumSize = new Vector2(0, 2);   // ความสูง 2px
		c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; // ยืดเต็มแนวนอน
		return c;   // ส่งกลับคอนโทรล
	}
	// สร้างช่องว่างแนวตั้งสูง h พิกเซล
	private Control MakeSpacer(int h)  
	{
		var c = new Control();   // คอนโทรลเปล่า
		c.CustomMinimumSize = new Vector2(0, h); // ตั้งความสูงขั้นต่ำ
		return c;    // ส่งกลับ
	}
	// สร้าง Label ข้อความทั่วไป (เช่น “ยังไม่มีข้อมูลอันดับ”)
	private Control MakeLabel(string text)  
	{
		var lbl = new Label { Text = text };   // สร้าง Label
		lbl.AddThemeFontSizeOverride("font_size", 22);  // ฟอนต์ 22
		lbl.AddThemeColorOverride("font_color", Colors.White);// สีขาว
		lbl.AddThemeConstantOverride("margin_left", 6);  // เว้นซ้ายเล็กน้อย
		return lbl;  // ส่งกลับ
	}
	
	// สร้างเซลล์ Label แบบยืด/ไม่ยืด และเลือกชิดซ้าย/ขวาได้
	private Control MakeCell(string text, bool expand, bool alignRight)                          
	{
		var lbl = new Label { Text = text, ClipText = true }; // Label โดยเปิด ClipText กันข้อความล้น
		lbl.HorizontalAlignment = alignRight ? HorizontalAlignment.Right : HorizontalAlignment.Left; // จัดแนวนอนซ้าย/ขวา
		lbl.AddThemeFontSizeOverride("font_size", 22);  // ฟอนต์ 22
		lbl.AddThemeColorOverride("font_color", Colors.White);  // สีขาว
		lbl.AddThemeConstantOverride("margin_left", 0);  // ไม่เพิ่ม margin ซ้าย
		lbl.CustomMinimumSize = new Vector2(42 * 5, RowH);  // กำหนด min size (กว้างคร่าว ๆ 210px, สูง RowH)
		lbl.SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill  // ถ้า expand = true ให้ยืดเต็มแนวนอน
										 : Control.SizeFlags.ShrinkBegin;  // ถ้าไม่ ให้ชิดซ้ายและไม่ยืด
		return lbl;                                                                              
	}
   
	// สร้างเซลล์ Label ความกว้างขั้นต่ำคงที่
	private Control MakeFixedCell(string text, int minW, bool alignRight)                        
	{
		var lbl = new Label { Text = text, ClipText = true };  // Label พร้อม ClipText
		lbl.HorizontalAlignment = alignRight ? HorizontalAlignment.Right : HorizontalAlignment.Left; // ชิดขวา/ซ้าย
		lbl.AddThemeFontSizeOverride("font_size", 22);  // ฟอนต์ 22
		lbl.AddThemeColorOverride("font_color", Colors.White);  // สีขาว
		lbl.AddThemeConstantOverride("margin_left", 0);  // ไม่เพิ่ม margin ซ้าย
		lbl.CustomMinimumSize = new Vector2(minW, RowH);// กำหนดความกว้างขั้นต่ำตาม minW และสูง RowH
		lbl.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;  // จัดให้ชิดขวาของกล่องที่ครอบ
		return lbl;  
		}                                                                   

	// ฟังก์ชันสร้างตัวคั่นแบบเส้นประ
	private Control MakeDashedSeparator(int height = 2, float alpha = 0.22f, float dash = 10f, float gap = 6f)
	{
		// สร้างอินสแตนซ์ของคอมโพเนนต์เส้นประ 
		return new DashedSeparator  
		{
			Thickness = height,// ตั้งความหนาเส้น
			Dash = dash, // ความยาวช่วงเส้น
			Gap = gap,// ความยาวช่องว่าง
			LineColor = new Color(1, 1, 1, alpha),  // สีเส้น (ขาวโปร่งตาม alpha)
			CustomMinimumSize = new Vector2(0, height + 2),  // ความสูงขั้นต่ำ (บวก 2 เผื่อ)
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill // ยืดเต็มแนวนอน
		};
	}
	 // เมธอดเรียกเมื่อกดปุ่มย้อนกลับ
	private void OnBackPressed()  
	{
		// สลับซีนไปหน้า StartGame
		GetTree().ChangeSceneToFile("res://SceneStartandHigh/StartGame.tscn");  
	}
}
