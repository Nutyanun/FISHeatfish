using Godot;                                  // ใช้คลาสหลักของเอนจิน Godot (Node, GD, ฯลฯ)
using System;                                  // เนมสเปซมาตรฐานของ C# (เผื่อใช้ชนิดข้อมูล/ยูทิล)

// ==== เพิ่มบรรทัดนี้ ====
using HUD = HudCard;   // ให้ชื่อ HUD ในไฟล์นี้ หมายถึงคลาส HudCard  // ตั้ง alias: เขียน HUD แทน HudCard ได้
// ========================

public partial class Main : Node               // คลาสหลักของซีน (รากตรรกะเกมในฉากนี้) สืบทอดจาก Node
{
	[Export] public NodePath ScoreManagerPath { get; set; } = "ScoreManager";  // พาธไปยังโหนด ScoreManager (แก้ได้ใน Inspector)
	[Export] public NodePath HudPath { get; set; } = "HudCard";   // ตั้งชื่อให้ตรงกับโหนดจริง   // พาธไปยัง HUD (HudCard) ในซีน

	private ScoreManager _sm;                 // ตัวแปรอ้างอิงไปยังโหนดจัดการคะแนน/เวลา/สถานะเกม
	private HUD _hud;  // ใช้ชื่อ HUD ได้ตามเดิม เพราะเราทำ alias ไว้แล้ว // อ้างอิงไปยัง HUD (ชนิดจริงคือ HudCard จาก alias)

	public override void _Ready()             // เรียกเมื่อโหนดนี้ถูกเพิ่มเข้าซีนและพร้อมทำงาน
	{
		_sm = GetNodeOrNull<ScoreManager>(ScoreManagerPath)       // พยายามหา ScoreManager ตาม NodePath ที่ตั้งไว้
		 ?? GetNodeOrNull<ScoreManager>("%ScoreManager");    // ถ้าไม่เจอ ลองหาแบบ unique name (%ScoreManager)

		_hud = GetNodeOrNull<HUD>(HudPath)                        // พยายามหา HUD (HudCard) ตาม NodePath ที่ตั้งไว้
		 ?? GetNodeOrNull<HUD>("%HudCard")                   // ถ้าไม่เจอ ลองหาแบบ unique name ชื่อ HudCard
		 ?? GetNodeOrNull<HUD>("%HUD"); // เผื่อคุณตั้งเป็น #HUD   // กันกรณีใช้ชื่อ unique name อื่น เช่น %HUD

		if (_hud == null)                                         // ตรวจว่าเจอ HUD ไหม
		{
		GD.PushError("[Main] HUD (HudCard) not found. ตั้ง HudPath ให้ตรง หรือทำโหนดเป็น #HudCard/#HUD"); // แจ้งเออเรอร์ชัดเจนในคอนโซล
		return;                                                // ยุติ _Ready() ไม่ดำเนินการต่อ เพราะไม่มี HUD จะเชื่อม
		}
		if (_sm == null)                                          // ตรวจว่าเจอ ScoreManager ไหม
		{
		GD.PushError("[Main] ScoreManager not found. ตั้ง ScoreManagerPath หรือทำเป็น #ScoreManager"); // แจ้งเออเรอร์ถ้าไม่พบ
		return;                                                // ยุติ _Ready() เพราะไม่มีตัวจัดการคะแนน/เวลา
		}
	}
}
