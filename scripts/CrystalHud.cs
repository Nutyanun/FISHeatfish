using Godot;                           // ใช้คลาสพื้นฐานของ Godot (Node, Control, Label, SubViewport ฯลฯ)
using System;                          // ฟังก์ชันพื้นฐาน .NET (เช่น Math)
using System.Collections.Generic;      // ใช้โครงสร้างข้อมูล Dictionary และ List

public partial class CrystalHud : Control // HUD แสดงบัพ/เอฟเฟกต์คริสตัลบนหน้าจอ
{
	[Export] public NodePath IconsContainerPath { get; set; } = null; // Path ไปยัง HBoxContainer ที่จะวางการ์ดไอคอนคริสตัล

	[Export] public PackedScene BlueScene  { get; set; }   // ซีนไอคอนคริสตัลสีฟ้า (Speed Up)
	[Export] public PackedScene GreenScene { get; set; }   // ซีนไอคอนคริสตัลสีเขียว (Invincible แบบ Stack)
	[Export] public PackedScene PinkScene  { get; set; }   // ซีนไอคอนคริสตัลสีชมพู (Coin Magnet)
	[Export] public PackedScene RedScene   { get; set; }   // ซีนไอคอนคริสตัลสีแดง (Time Shift/เพิ่มเวลา)
	[Export] public PackedScene PurpleScene{ get; set; }   // ซีนไอคอนคริสตัลสีม่วง (Score Boost คูณคะแนนชั่วคราว)

	[Export] public Vector2I IconSize = new Vector2I(64,64); // ขนาดไอคอนที่เรนเดอร์ใน HUD
	[Export] public float Node2DScale = 0.5f;                // สเกลสำหรับซีนที่เป็น Node2D เพื่อให้พอดีกับไอคอน
	[Export] public int TopPadding = 5;                      // ระยะห่างจากขอบบนหน้าจอ

	[Export] public Label CrystalCountLabel { get; set; }   // Label แสดงจำนวนคริสตัลรวม (ลากเชื่อมใน Inspector)

	private HBoxContainer _box;                              // กล่องแนวนอนเก็บการ์ดไอคอนทั้งหมด

	private class Entry                                      // โครงสร้างเก็บสถานะของคริสตัล 1 ชนิดบน HUD
	{
		public CrystalType Type;     // ชนิดของคริสตัล
		public Control CardRoot;     // การ์ด (VBoxContainer/Container) ที่ครอบ icon + label
		public Label Label;          // Label แสดงเวลาคงเหลือหรือจำนวนสแตก
		public float TimeLeft;       // เวลาคงเหลือ (วินาที) ถ้า < 0 หมายถึงไม่มีตัวจับเวลา (เช่น เขียว)
		public int Count;            // จำนวนสแตก (ใช้กับสีเขียว และใช้เป็นตัวคูณแสดงในม่วง)
	}
	private readonly Dictionary<CrystalType, Entry> _entries = new(); // ตารางคริสตัลที่กำลังโชว์บน HUD

	public override void _Ready()                        // เริ่มทำงานเมื่อโหนดถูกเพิ่มเข้า Scene Tree
	{
		_box = (IconsContainerPath != null && !IconsContainerPath.IsEmpty) // ถ้ามี Path ของ container ใน Inspector
			? GetNodeOrNull<HBoxContainer>(IconsContainerPath)             // ใช้ Path เพื่อดึง HBoxContainer
			: GetNodeOrNull<HBoxContainer>("HBox");                        // ไม่งั้นลองค้นหาชื่อ "HBox" ใต้ HUD นี้

		if (_box == null)                                                // ถ้ายังหาไม่เจอจริง ๆ
		{
			_box = new HBoxContainer { Name = "HBox" };                  // สร้าง HBoxContainer ใหม่
			AddChild(_box);                                              // เพิ่มเป็นลูกของ HUD
		}

		MouseFilter = MouseFilterEnum.Ignore;                            // ไม่ให้ HUD กินอีเวนต์เมาส์ (คลิกทะลุได้)
		ProcessMode = ProcessModeEnum.Always;                            // HUD อัปเดตแม้เกม pause (เช่น นับถอยหลังต่อ)

		Position = new Vector2(Position.X, Mathf.Max(TopPadding, Position.Y)); // ขยับ HUD ลงมาตาม TopPadding

		if (CrystalCountLabel != null)                                   // ถ้ามี Label แสดงจำนวนรวม
			CrystalCountLabel.Position += new Vector2(0, -20);           // ยกขึ้นเล็กน้อยให้จัดวางสวยขึ้น
	}

	public override void _Process(double delta)                          // อัปเดตทุกเฟรม (นับเวลาถอยหลังบัพ)
	{
		var remove = new List<CrystalType>();                            // รายการชนิดคริสตัลที่หมดเวลา ต้องลบทิ้ง

		foreach (var kv in _entries)                                     // วนดู entry ทุกตัวที่กำลังแสดง
		{
			var e = kv.Value;                                            // entry ปัจจุบัน
			if (e.TimeLeft >= 0f)                                        // ถ้ามีตัวจับเวลา (เช่น ฟ้า/ชมพู/แดง/ม่วง)
			{
				e.TimeLeft -= (float)delta;                              // ลดเวลาตาม delta
				if (e.TimeLeft <= 0f)                                    // ถ้าหมดเวลาแล้ว
				{
					remove.Add(e.Type);                                   // คิวลบออกภายหลัง
				}
				else                                                     // ยังไม่หมดเวลา
				{
					if (e.Type == CrystalType.Purple)                    // ม่วงต้องโชว์รูปแบบ "*N วินาที"
						e.Label.Text = $"*{Math.Max(1, e.Count)} {Mathf.Ceil(e.TimeLeft)}"; // *count เวลา
					else                                                  // สีอื่น ๆ โชว์เฉพาะวินาที
						e.Label.Text = $"{Mathf.Ceil(e.TimeLeft)}";       // ปัดเศษขึ้นให้ดูเคาน์ดาวน์
				}
			}
			// สีเขียวไม่มีตัวจับเวลา: แสดงด้วย Count เพียงอย่างเดียว จัดการตอนเพิ่ม/ลบ
		}

		foreach (var t in remove)                                         // วนลบรายการที่หมดเวลา
			ClearBuff(t);                                                 // เคลียร์การ์ดบัพออกจาก HUD
	}

	public void ShowBuff(CrystalType type, float durationSec = -1f, string labelOverride = null, int? countOverride = null) // แสดง/อัปเดตการ์ดบัพ
	{
		if (_entries.TryGetValue(type, out var exist))                   // ถ้ามีการ์ดชนิดนี้อยู่แล้ว
		{
			if (type == CrystalType.Green && durationSec < 0f)           // สีเขียว: ไม่มีเวลา → เพิ่มจำนวนสแตก
			{
				exist.Count++;                                            // +1 สแตก
				exist.Label.Text = $"x{exist.Count}";                     // อัปเดตข้อความเป็น xN
				return;                                                   // จบการอัปเดต
			}
			else                                                          // บัพที่มีเวลา หรืออยากรีเฟรชเวลา
			{
				exist.TimeLeft = durationSec;                             // รีเซ็ตเวลาคงเหลือ

				if (type == CrystalType.Purple && durationSec >= 0f)      // ม่วง: แสดงรูปแบบพิเศษ
				{
					exist.Count = Math.Max(1, exist.Count);               // อย่างน้อย *1
					exist.Label.Text = labelOverride ?? $"*{exist.Count} {Mathf.Ceil(durationSec)}"; // ใช้ override ถ้ามี
				}
				else                                                      // สีอื่น ๆ
				{
					exist.Label.Text = labelOverride ?? (durationSec >= 0 ? $"{Mathf.Ceil(durationSec)}" : ""); // เวลา/override
				}
			}
			return;                                                       // อัปเดตการ์ดเดิมเสร็จ
		}

		var card = new VBoxContainer                                    // ยังไม่มีการ์ดชนิดนี้ → สร้างใหม่
		{
			CustomMinimumSize   = new Vector2(IconSize.X + 4, IconSize.Y + 12), // ขนาดขั้นต่ำของการ์ด (เผื่อ label)
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,                       // จัดให้อยู่กลางแนวนอน
			SizeFlagsVertical   = SizeFlags.ShrinkCenter                        // จัดให้อยู่กลางแนวตั้ง
		};

		Control visual = MakeCrystalVisual(type);                         // สร้างคอมโพเนนต์ภาพคริสตัลตามชนิด
		visual.CustomMinimumSize   = new Vector2(IconSize.X, IconSize.Y); // บังคับขนาดเท่ากรอบไอคอน
		visual.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;              // จัดตำแหน่งกลางแนวนอน
		visual.SizeFlagsVertical   = SizeFlags.ShrinkCenter;              // จัดตำแหน่งกลางแนวตั้ง

		var label = new Label                                            // Label ด้านล่างใช้โชว์เวลา/ข้อความ
		{
			Text                 = durationSec >= 0 ? $"{Mathf.Ceil(durationSec)}" : "", // ถ้ามีเวลา → แสดงเป็นวินาที
			HorizontalAlignment  = HorizontalAlignment.Center,                            // จัดกลาง
			VerticalAlignment    = VerticalAlignment.Center,                              // จัดกลาง
			SizeFlagsHorizontal  = SizeFlags.ShrinkCenter                                 // จัดกลางแนวนอน
		};
		label.AddThemeFontSizeOverride("font_size", 14);                  // ตั้งขนาดฟอนต์ให้ชัดเจน

		card.AddChild(visual);                                            // ใส่ภาพลงในการ์ด
		card.AddChild(label);                                             // ใส่ label ลงในการ์ด
		_box.AddChild(card);                                              // เพิ่มการ์ดลงใน HBox บน HUD

		var eNew = new Entry                                              // สร้างข้อมูลสถานะสำหรับการ์ดใหม่นี้
		{
			Type     = type,                                              // ชนิดคริสตัล
			CardRoot = card,                                              // อ้างถึงการ์ด
			Label    = label,                                             // อ้างถึง label
			TimeLeft = durationSec,                                       // เก็บเวลาเริ่มต้น (-1 = ไม่มี)
			Count    = 0                                                  // เริ่ม count เป็น 0
		};

		if (type == CrystalType.Green && durationSec < 0f)                // เขียว: ไม่มีเวลา → เริ่ม x1
		{
			eNew.Count = 1;                                               // จำนวนสแตกเริ่มต้น
			eNew.Label.Text = "x1";                                       // แสดงผล x1
		}

		if (type == CrystalType.Purple && durationSec >= 0f)              // ม่วง: มีเวลา → ต้องโชว์ *1 เวลา
		{
			eNew.Count      = 1;                                          // ตั้ง count เริ่มต้น
			eNew.Label.Text = labelOverride ?? $"*{eNew.Count} {Mathf.Ceil(durationSec)}"; // override ถ้ามี
		}

		if (type == CrystalType.Red && !string.IsNullOrEmpty(labelOverride)) // แดง: ถ้ากำหนดข้อความเอง เช่น "+10s"
		{
			eNew.Label.Text = labelOverride;                               // ใช้ข้อความนั้นตรง ๆ
		}

		_entries[type] = eNew;                                            // บันทึก entry ใหม่เข้าตาราง
	}

	public void ClearBuff(CrystalType type)                               // ลบบัพชนิดหนึ่งออกจาก HUD
	{
		if (!_entries.TryGetValue(type, out var e)) return;               // ถ้าไม่มีในตาราง → ไม่ทำอะไร

		if (type == CrystalType.Green && e.TimeLeft < 0f)                 // สีเขียว: ลดจำนวนสแตกลงทีละ 1
		{
			e.Count = Math.Max(0, e.Count - 1);                           // อย่าให้ติดลบ
			if (e.Count > 0)                                              // ถ้ายังเหลือสแตกอยู่
			{
				e.Label.Text = $"x{e.Count}";                             // อัปเดตข้อความแล้วจบ
				return;                                                   // ไม่ต้องลบการ์ดทั้งใบ
			}
			// ถ้าเหลือ 0 → ไปต่อเพื่อลบการ์ด
		}

		e.CardRoot.QueueFree();                                           // สั่งลบการ์ดออกจาก Scene
		_entries.Remove(type);                                            // ลบออกจากตารางสถานะ
	}

	private Control MakeCrystalVisual(CrystalType type)                   // สร้างคอมโพเนนต์ภาพของคริสตัลให้พอดีกับ HUD
	{
		var scene = SceneOf(type);                                        // เลือกซีนตามชนิด
		if (scene == null)                                                // ถ้าไม่ได้กำหนดซีนไว้
		{
			var l = new Label                                             // ใช้ Label แทนรูปภาพ
			{
				Text = ShortName(type),                                   // ชื่อย่อสี เช่น B, G, P, R, Pu
				HorizontalAlignment = HorizontalAlignment.Center,         // จัดกลาง
				VerticalAlignment   = VerticalAlignment.Center            // จัดกลาง
			};
			l.AddThemeFontSizeOverride("font_size", 16);                  // ตั้งฟอนต์ให้มองเห็นชัด
			return l;                                                     // คืนคอมโพเนนต์ Label
		}

		Node inst = scene.Instantiate();                                  // มีซีนจริง → สร้างอินสแตนซ์

		if (inst is Control ctrl)                                         // ถ้ารากเป็น Control (UI)
		{
			ctrl.MouseFilter = MouseFilterEnum.Ignore;                    // ไม่กินอีเวนต์เมาส์
			var holder = new MarginContainer();                           // ครอบด้วย MarginContainer ให้คุมขนาดง่าย
			holder.AddChild(ctrl);                                        // ใส่ ctrl เข้าไปใน holder
			return holder;                                                // คืน holder กลับไปเป็น visual
		}
		else                                                              // ถ้าเป็น Node2D (ฉาก 2D)
		{
			var vp = new SubViewport                                      // สร้าง SubViewport ไว้เรนเดอร์ 2D เป็น Texture
			{
				TransparentBg = true,                                     // พื้นหลังโปร่งใส
				RenderTargetClearMode = SubViewport.ClearMode.Always,     // เคลียร์ทุกเฟรมป้องกันภาพค้าง
				Size = new Vector2I(IconSize.X, IconSize.Y)               // ขนาดเท่ากรอบไอคอน
			};
			var root2D = new Node2D();                                    // สร้างราก 2D สำหรับวาง inst
			vp.AddChild(root2D);                                          // เพิ่ม root2D เข้า viewport
			root2D.AddChild(inst);                                        // วาง inst ลงใน root2D

			if (inst is Node2D n2d)                                       // ถ้าตัว inst เป็น Node2D จริง
			{
				n2d.Scale = new Vector2(Node2DScale, Node2DScale);        // ย่อ/ขยายให้พอดีเฟรม
				n2d.Position = Vector2.Zero;                              // วางไว้ที่ (0,0) ใน viewport
			}

			var vc = new SubViewportContainer                             // สร้าง container UI สำหรับโชว์ viewport
			{
				Stretch = true,                                           // ขยายภาพให้เต็มพื้นที่
				Size = new Vector2(IconSize.X, IconSize.Y)                // ขนาดเท่ากรอบไอคอน
			};
			vc.AddChild(vp);                                              // ใส่ viewport ลงใน container
			return vc;                                                    // คืนคอมโพเนนต์ container เป็น visual
		}
	}

	private PackedScene SceneOf(CrystalType t) => t switch               // เลือกซีนของไอคอนตามชนิดคริสตัล
	{
		CrystalType.Blue   => BlueScene,                                  // ฟ้า
		CrystalType.Green  => GreenScene,                                 // เขียว
		CrystalType.Pink   => PinkScene,                                  // ชมพู
		CrystalType.Red    => RedScene,                                   // แดง
		CrystalType.Purple => PurpleScene,                                 // ม่วง
		_ => null                                                         // อื่น ๆ → ไม่มีซีน
	};

	private string ShortName(CrystalType t) => t switch                  // ชื่อย่อใช้ตอน fallback เป็น Label
	{
		CrystalType.Blue   => "B",                                        // ฟ้า → B
		CrystalType.Green  => "G",                                        // เขียว → G
		CrystalType.Pink   => "P",                                        // ชมพู → P
		CrystalType.Red    => "R",                                        // แดง → R
		CrystalType.Purple => "Pu",                                       // ม่วง → Pu
		_ => "?"                                                          // อื่น ๆ → ?
	};

	public void SetCrystalCount(int count)                                // อัปเดตจำนวนคริสตัลรวมทั้งหมด
	{
		if (CrystalCountLabel != null)                                    // ถ้ามี Label ให้แสดง
			CrystalCountLabel.Text = "x" + count;                         // ตั้งข้อความเป็นรูปแบบ xN
	}
}
