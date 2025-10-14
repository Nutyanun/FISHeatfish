using Godot;
using System;
// ==== เพิ่มบรรทัดนี้ ====
using HUD = HudCard;   // ให้ชื่อ HUD ในไฟล์นี้ หมายถึงคลาส HudCard
// ========================
public partial class Main : Node
{
	[Export] public NodePath ScoreManagerPath { get; set; } = "ScoreManager";
	[Export] public NodePath HudPath { get; set; } = "HudCard";   // ตั้งชื่อให้ตรงกับโหนดจริง

	private ScoreManager _sm;
	private HUD _hud;  // ใช้ชื่อ HUD ได้ตามเดิม เพราะเราทำ alias ไว้แล้ว

	public override void _Ready()
	{
		_sm = GetNodeOrNull<ScoreManager>(ScoreManagerPath)
			  ?? GetNodeOrNull<ScoreManager>("%ScoreManager");

		_hud = GetNodeOrNull<HUD>(HudPath)
			  ?? GetNodeOrNull<HUD>("%HudCard")
			  ?? GetNodeOrNull<HUD>("%HUD"); // เผื่อคุณตั้งเป็น #HUD

		if (_hud == null)
		{
			GD.PushError("[Main] HUD (HudCard) not found. ตั้ง HudPath ให้ตรง หรือทำโหนดเป็น #HudCard/#HUD");
			return;
		}
		if (_sm == null)
		{
			GD.PushError("[Main] ScoreManager not found. ตั้ง ScoreManagerPath หรือทำเป็น #ScoreManager");
			return;
		}

		// ไม่ต้อง connect อะไรเพิ่ม HudCard เชื่อมกับ ScoreManager เองแล้ว
	}
}
