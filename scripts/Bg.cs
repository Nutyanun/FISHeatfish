using Godot;          // ใช้ API ของ Godot เช่น Node, ParallaxBackground, Vector2
using Game;           // ใช้ namespace ภายในโปรเจกต์ (ถ้ามีจริง)

// สร้าง namespace Game (เพื่อจัดระเบียบสคริปต์ของเกม)
namespace Game
{
	// ==============================
	// Enum: CrystalType
	// ==============================
	// ใช้ระบุประเภทของ “คริสตัล” ในเกมแต่ละสี
	// เพื่อให้เรียกใช้แบบอ่านง่ายแทนการพิมพ์ string เช่น CrystalType.Blue
	public enum CrystalType
	{
		Blue = 0,    // คริสตัลสีฟ้า → เพิ่มความเร็ว
		Red = 1,     // คริสตัลสีแดง → เพิ่มเวลา
		Green = 2,   // คริสตัลสีเขียว → คุ้มกันจากอันตราย (Invincible)
		Pink = 3,    // คริสตัลสีชมพู → ดึงเหรียญ (Coin Magnet)
		Purple = 4,  // คริสตัลสีม่วง → บูสต์คะแนน (Score Boost)
	}
	
	// ==============================
	// คลาส Bg: ควบคุมพื้นหลังแบบเลื่อน (ParallaxBackground)
	// ==============================
	public partial class Bg : ParallaxBackground 
	{
		// ความเร็วของการเลื่อนฉาก (X = แนวนอน, Y = แนวตั้ง)
		// ค่าเริ่มต้นคือเลื่อนไปทางซ้าย (-120f)
		[Export] public Vector2 Speed = new(-120f, 0f);

		// ฟังก์ชันนี้จะถูกเรียกเมื่อ Node ถูกโหลดขึ้นมาในฉาก
		public override void _Ready()
		{
			// ให้พื้นหลังยังคงเลื่อนต่อได้ แม้เกมจะอยู่ในสถานะ pause
			// เช่น ตอนเปิด PauseUI แต่ฉากพื้นหลังยังเคลื่อนไหว
			ProcessMode = Node.ProcessModeEnum.Always;
		}

		// ฟังก์ชันนี้จะถูกเรียกทุกเฟรม (Frame Update)
		// delta = เวลาที่ผ่านไปต่อเฟรม (ใช้ให้เคลื่อนไหวลื่น)
		public override void _Process(double delta)
		{
			// ScrollOffset คือการเลื่อนตำแหน่งของพื้นหลัง
			// เอาความเร็ว (Speed) คูณกับเวลา → ทำให้พื้นหลังเคลื่อนไหวต่อเนื่อง
			ScrollOffset += Speed * (float)delta;
		}
	}
}
