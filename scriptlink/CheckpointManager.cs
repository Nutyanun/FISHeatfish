using Godot;
using System.Linq;

public partial class CheckpointManager : Node2D
{
	public override void _Ready()
	{
		GameProgress.Load();   // โหลดข้อมูลความคืบหน้าของผู้เล่น (เช่นถึงด่านไหนแล้ว)
		UpdateLevelVisual();   // อัปเดตรูปภาพหรือสีของด่านตามข้อมูลที่โหลดมา
	}

	private void UpdateLevelVisual()
	{
		// ดึงลูกทั้งหมดของ Node นี้ที่เป็นคลาส Level
		// แล้วเรียงลำดับตามตัวเลขท้ายชื่อ (Level1, Level2, ... )
		var Levels = GetChildren()
			.OfType<Level>()  // เอาเฉพาะ node ที่เป็น Level
			.OrderBy(m => ExtractNumber(m.Name.ToString())) // เรียงตามหมายเลขท้ายชื่อ
			.ToList(); // แปลงเป็น List

		// วนทุกด่านเพื่อเช็กสถานะว่าผ่านแล้ว / ปัจจุบัน / ยังไม่ถึง
		for (int idx = 0; idx < Levels.Count; idx++)
		{
			var script = Levels[idx]; // ดึง Level ปัจจุบันจากลิสต์

			if (idx < GameProgress.CurrentLevelIndex)
			{
				// ถ้าด่านนี้อยู่ก่อนด่านปัจจุบัน → แสดงว่า "ผ่านแล้ว"
				script.SetDone(true);              // ตั้งสถานะว่าเสร็จแล้ว
				script.SetActive(false);           // ไม่สามารถเลือกได้
				script.SelfModulate = new Color(1f, 0.4f, 0.4f); // ทำให้เป็นสีแดง (บอกว่าผ่านแล้ว)
			}
			else if (idx == GameProgress.CurrentLevelIndex)
			{
				// ถ้าเป็นด่านปัจจุบัน → ให้เลือกได้ (active)
				script.SetActive(true);            // เปิดให้คลิกเข้าเล่นได้
				script.SetDone(false);             // ยังไม่ถือว่าผ่าน
				script.SelfModulate = new Color(1, 1, 1); // สีปกติ (หรือจะทำให้วิบวับได้)
			}
			else
			{
				// ถ้าเป็นด่านที่ยังไม่ถึง → ยังล็อกอยู่
				script.SetDone(false);
				script.SetActive(false);           // ยังเล่นไม่ได้
				script.SelfModulate = new Color(0.6f, 0.6f, 0.6f); // ทำให้ซีดลง (บอกว่ายังล็อก)
			}
		}
	}

	private int ExtractNumber(string name)
	{
		// ดึงเฉพาะตัวเลขจากชื่อ Node (เช่น "Level3" → 3)
		string digits = new string(name.Where(char.IsDigit).ToArray());
		return string.IsNullOrEmpty(digits) ? 0 : int.Parse(digits);
	}
	
	public void Refresh()
	{
		GameProgress.Load();     // โหลดข้อมูลความคืบหน้าใหม่ (กรณีเพิ่งผ่านด่าน)
		UpdateLevelVisual();     // อัปเดตภาพของด่านใหม่ให้ตรงกับข้อมูลล่าสุด
	}
}
