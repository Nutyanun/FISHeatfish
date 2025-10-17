using Godot;                                         // ใช้ API ของ Godot (Node2D, Label, RichTextLabel, ฯลฯ)
using System;                                        // .NET พื้นฐาน (เช่น Math)
using System.Text;                                   // ใช้ StringBuilder สำหรับต่อข้อความอย่างมีประสิทธิภาพ
using GDict = Godot.Collections.Dictionary;          // ตั้งชื่อย่อให้ Dictionary ของ Godot (อ่านง่ายขึ้น)
using System.Collections.Generic;                    // ใช้สำหรับ List<string> และโครงสร้างข้อมูลทั่วไป

// คลาส ScoreScene ใช้ในหน้าสรุปคะแนนหลังจบด่าน
public partial class ScoreScene : Node2D
{
	// ======= ตัวแปร Export จาก Godot Inspector =======
	[Export] public Label TitleLabel { get; set; }        // หัวข้อ "Level X Summary"
	[Export] public Label ScoreLabel { get; set; }        // คะแนนหลัก (Score)
	[Export] public Label HighScoreLabel { get; set; }    // คะแนนสูงสุดส่วนตัว (High Score)
	[Export] public RichTextLabel FishSummary { get; set; } // ช่องสรุปจำนวนปลาที่กินได้ (RichTextLabel)
	[Export] public Label BonusLabel { get; set; }        // คะแนนโบนัส (Bonus)
	[Export] public Label TotalLabel { get; set; }        // คะแนนรวม (Total)

	// ฟังก์ชันจะถูกเรียกอัตโนมัติเมื่อซีนถูกโหลดและพร้อมใช้งาน
	public override void _Ready()
	{
		// แสดงข้อมูลในคอนโซลเพื่อ debug ว่าค่าจาก GameProgress ถูกต้องไหม
		GD.Print($"[ScoreScene] Read from GameProgress → Score={GameProgress.LastLevelScore}, Bonus={GameProgress.LastBonusScore}, Total={GameProgress.LastTotalScore}, High={GameProgress.LastHighScore}");

		// ดึงข้อมูลพื้นฐานจาก GameProgress (ซิงเกิลตันเก็บสถานะเกม)
		int levelIndex = GameProgress.CurrentPlayingLevel; // ด่านที่เพิ่งเล่นจบ
		int score = GameProgress.LastLevelScore;           // คะแนนหลัก
		int bonus = GameProgress.LastBonusScore;           // คะแนนโบนัส
		int total = GameProgress.LastTotalScore;           // คะแนนรวม (score + bonus)

		// ดึงชื่อผู้เล่นจากระบบล็อกอิน (หรือ "Guest" ถ้าไม่มี)
		string user = CurrentUserName();

		// ดึงคีย์วันที่ (ใช้เป็นชื่อเซฟคะแนนใน Leaderboard)
		string dateKey = PlayerLogin.Instance?.TodayKey
						 ?? LeaderboardStore.MakeTodayKeyLocal(); // ถ้าไม่มี PlayerLogin ให้ใช้เวลาปัจจุบันแทน

		// บันทึกคะแนนลงไฟล์ Leaderboard
		LeaderboardStore.UpsertScore(
			dateKey, levelIndex, user, total, limitTop: 50 // จำกัดแค่ 50 อันดับต่อด่านต่อวัน
		);

		// โหลดคะแนนสูงสุดส่วนตัวของผู้เล่นในด่านนี้
		int personalHigh = LoadPersonalHigh(levelIndex, user);

		// ======= ตั้งค่าข้อความใน Label ต่าง ๆ =======
		TitleLabel     ?.SetText($"Level {levelIndex} Summary");  // หัวข้อ
		ScoreLabel     ?.SetText($"Score: {score}");              // คะแนนหลัก

		// แสดง Bonus และ Total เฉพาะด่าน 2 ขึ้นไป (ด่านแรกไม่มีระบบโบนัส)
		if (levelIndex >= 2)
		{
			BonusLabel.Visible = true;
			TotalLabel.Visible = true;
			BonusLabel.SetText($"Bonus: {bonus}");
			TotalLabel.SetText($"Total: {total}");
		}
		else
		{
			BonusLabel.Visible = false;
			TotalLabel.Visible = false;
		}

		// แสดง High Score ส่วนตัว (ค่าสูงสุดระหว่างรอบนี้กับเดิม)
		HighScoreLabel?.SetText($"High Score: {Math.Max(personalHigh, total)}");

		// ======= เรียกฟังก์ชันโชว์ UI ปลาที่กินได้ =======
		ShowFishIconsByLevel();   // ซ่อน/โชว์ภาพปลาตามด่าน
		ShowFishSummary();        // แสดงจำนวนปลาที่กินได้ในแต่ละประเภท

		// พิมพ์ log ดีบักอีกครั้ง (ไว้ตรวจผลรวมทั้งหมด)
		GD.Print($"[ScoreScene] L{levelIndex} Score={score} Bonus={bonus} Total={total} User={user} DateKey={dateKey}");
	}

	// ============================
	//  ฟังก์ชันเลือก HBox ที่จะโชว์ (กลุ่มปลาแต่ละด่าน)
	// ============================
	private void ShowFishIconsByLevel()
	{
		int level = GameProgress.CurrentPlayingLevel; // ด่านที่เพิ่งเล่น

		// ดึง HBoxContainer สองกล่องจาก Scene (ต้องตั้งชื่อ Node ให้ตรงใน Godot)
		var hboxLv1_2 = GetNodeOrNull<HBoxContainer>("%HBoxContainer");    // สำหรับด่าน 1–2
		var hboxLv3_7 = GetNodeOrNull<HBoxContainer>("%HBoxContainer_Lv3"); // สำหรับด่าน 3–7

		// ถ้าไม่เจอ Node ใด Node หนึ่ง ให้แจ้ง error แล้วหยุด
		if (hboxLv1_2 == null || hboxLv3_7 == null)
		{
			GD.PrintErr("⚠️ ไม่เจอ HBoxContainer บางอันใน Scene! ตรวจชื่อ Node ให้ตรง");
			return;
		}

		// ถ้าอยู่ในด่าน 1 หรือ 2 → แสดง HBox ของกลุ่มแรก
		if (level <= 2)
		{
			hboxLv1_2.Visible = true;
			hboxLv3_7.Visible = false;
		}
		// ถ้าอยู่ในด่าน 3 ขึ้นไป → แสดง HBox ของกลุ่มที่สอง
		else
		{
			hboxLv1_2.Visible = false;
			hboxLv3_7.Visible = true;
		}
	}

	// ============================
	// ฟังก์ชันสร้างข้อความสรุปจำนวนปลาที่กินได้
	// ============================
	private void ShowFishSummary()
	{
		if (FishSummary == null) return;  // ถ้า RichTextLabel ยังไม่ถูกเชื่อม → ออก

		// ถ้ายังไม่มีข้อมูลปลา → แสดงค่า 0 ทั้งหมด
		if (GameProgress.FishCountByType == null || GameProgress.FishCountByType.Count == 0)
		{
			FishSummary.Text = "0\t0\t0\t0";
			return;
		}

		// ดึงด่านปัจจุบันที่เล่น
		int level = GameProgress.CurrentPlayingLevel;

		// ลำดับการแสดงผลปลาพื้นฐาน (ด่าน 1–2)
		var order = new List<string> { "fish2", "fish3", "fish1", "shark" };

		// ถ้าเป็นด่าน 3 ขึ้นไป → เพิ่มปลาพิเศษอีก 2 ชนิด
		if (level >= 3)
		{
			order.Add("SawShark");   // ปลาฉลามเลื่อย
			order.Add("Seaangler");  // ปลาแองเกลอร์
		}

		// ใช้ StringBuilder ต่อข้อความตัวเลขปลาแต่ละชนิด
		var sb = new StringBuilder();
		foreach (var t in order)
		{
			int c = GameProgress.FishCountByType.ContainsKey(t)
				? GameProgress.FishCountByType[t]  // ถ้ามีชนิดนี้ใน dict → ดึงค่า
				: 0;                               // ถ้าไม่มี → ใส่ 0
			sb.Append(c).Append('\t');             // คั่นแต่ละค่าให้เรียงในบรรทัดเดียว
		}

		FishSummary.BbcodeEnabled = false;         // ปิด BBCode (ใช้ text ธรรมดา)
		FishSummary.Text = sb.ToString().TrimEnd(); // แสดงข้อความที่สร้างเสร็จ
	}

	// ============================
	// โหลดคะแนนสูงสุดส่วนตัวจาก Leaderboard
	// ============================
	private int LoadPersonalHigh(int levelIndex, string user)
	{
		var doc = LeaderboardStore.LoadDoc();  // โหลดไฟล์ leaderboard ทั้งหมด

		if (!doc.ContainsKey("players")) return 0;   // ไม่มีผู้เล่นเลย → 0
		var players = (GDict)doc["players"];
		if (!players.ContainsKey(user)) return 0;    // ไม่มีชื่อผู้เล่นนี้ → 0

		var p = (GDict)players[user];
		if (!p.ContainsKey("levels")) return 0;      // ไม่มีข้อมูลระดับเลเวล → 0
		var lv = (GDict)p["levels"];

		var key = levelIndex.ToString();             // แปลงเลขด่านเป็น string key
		if (!lv.ContainsKey(key)) return 0;          // ยังไม่เคยเล่นด่านนี้ → 0

		var obj = (GDict)lv[key];                    // ดึงข้อมูลของด่านนี้
		return obj.ContainsKey("high")               // ถ้ามีคีย์ high → ดึงค่า
			   ? (int)(long)obj["high"]              // แปลงจาก long → int (เพราะ JSON อ่านเป็น long)
			   : 0;                                  // ไม่มี → 0
	}

	// ============================
	// ดึงชื่อผู้เล่นปัจจุบัน (ถ้าไม่มี = Guest)
	// ============================
	private string CurrentUserName()
	{
		try
		{
			var n = PlayerLogin.Instance?.CurrentUser?.PlayerName; // พยายามดึงจากระบบล็อกอิน
			return string.IsNullOrEmpty(n) ? "Guest" : n;          // ถ้าว่าง → Guest
		}
		catch { return "Guest"; }                                 // ถ้ามี error → Guest
	}

	// ============================
	// อินพุตตอนจบเกม (กด Enter หรือ ESC เพื่อกลับเมนู)
	// ============================
	public override void _UnhandledInput(InputEvent e)
	{
		// ถ้าผู้เล่นกดปุ่มยืนยันหรือยกเลิก (Enter, Space, หรือ Esc)
		if (e.IsActionPressed("ui_accept") || e.IsActionPressed("ui_cancel"))
		{
			// ถ้าด่านที่เล่น = ด่านล่าสุด + 1 → ผ่านด่านนี้จริง ให้ Advance()
			if (GameProgress.CurrentPlayingLevel == GameProgress.CurrentLevelIndex + 1)
				GameProgress.Advance();  // ปลดล็อกด่านถัดไป

			// กลับไปยังหน้าเลือกด่าน (Checkpoint Scene)
			GetTree().ChangeSceneToFile("res://scenecheckpoint/checkpoint.tscn");
		}
	}
}
