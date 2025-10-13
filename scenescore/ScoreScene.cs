using Godot;                                         // ใช้ API ของ Godot (Node2D, Label, RichTextLabel, ฯลฯ)
using System;                                        // .NET พื้นฐาน (เช่น Math)
using System.Text;                                   // ใช้ StringBuilder ต่อสตริงอย่างมีประสิทธิภาพ
using GDict = Godot.Collections.Dictionary;          // ตั้งชื่อสั้นให้ Godot.Collections.Dictionary (ชนิด dynamic ใน Godot)

public partial class ScoreScene : Node2D             // ซีนสรุปคะแนนหลังจบด่าน สืบทอดจาก Node2D
{
	[Export] public Label TitleLabel { get; set; }        // [Export] อ้างถึง Label หัวข้อ "Level X Summary"
	[Export] public Label ScoreLabel { get; set; }        // [Export] อ้างถึง Label แสดงคะแนนดิบ (score)
	[Export] public Label HighScoreLabel { get; set; }    // [Export] อ้างถึง Label แสดงสถิติส่วนตัวสูงสุด
	[Export] public RichTextLabel FishSummary { get; set; } // [Export] อ้างถึง RichTextLabel แสดงสรุปจำนวนปลา
	[Export] public Label BonusLabel { get; set; }        // [Export] อ้างถึง Label แสดงโบนัส
	[Export] public Label TotalLabel { get; set; }        // [Export] อ้างถึง Label แสดงคะแนนรวม (total)

	public override void _Ready()                         // เรียกเมื่อซีนพร้อมใช้งาน
	{
		int levelIndex = GameProgress.CurrentPlayingLevel; // ด่านที่เพิ่งเล่นจบ (index)
		int score = GameProgress.LastLevelScore;           // คะแนนหลักของด่าน
		int bonus = GameProgress.LastBonusScore;           // คะแนนโบนัส
		int total = GameProgress.LastTotalScore;           // คะแนนรวม (score + bonus หรือสูตรที่เกมกำหนด)

		string user = CurrentUserName();                   // ชื่อผู้เล่นปัจจุบัน (ถ้าไม่มี → "Guest")
		string dateKey = PlayerLogin.Instance?.TodayKey    // คีย์วันที่จากระบบล็อกอิน (Autoload)
						 ?? LeaderboardStore.MakeTodayKeyLocal(); // เผื่อกรณีไม่มี PlayerLogin ให้ใช้เวลาปัจจุบัน
		LeaderboardStore.UpsertScore(                      // อัปเซิร์ตสกอร์ลงไฟล์ leaderboards/players
			dateKey, levelIndex, user, total, limitTop: 50 // จำกัดอันดับบนสุดต่อเลเวล/วันไว้ 50 รายการ
		);

		int personalHigh = LoadPersonalHigh(levelIndex, user); // โหลดสถิติสูงสุดส่วนตัวของด่านนี้

		TitleLabel     ?.SetText($"Level {levelIndex} Summary");  // ตั้งหัวข้อสรุปผล
		ScoreLabel     ?.SetText($"Score: {score}");              // แสดงคะแนนดิบ
		BonusLabel     ?.SetText($"Bonus: {bonus}");              // แสดงโบนัส
		TotalLabel     ?.SetText($"Total: {total}");              // แสดงคะแนนรวม
		HighScoreLabel ?.SetText($"High Score: {Math.Max(personalHigh, total)}"); // แสดงค่าสูงสุดระหว่าง high เดิมกับรอบนี้

		ShowFishSummary();                                     // แสดงสรุปจำนวนปลาตามชนิด

		GD.Print($"[ScoreScene] L{levelIndex} Score={score} Bonus={bonus} Total={total} User={user} DateKey={dateKey}");
		// พิมพ์ดีบักลงคอนโซล
	}

	private void ShowFishSummary()                            // สร้างข้อความสรุปจำนวนปลาต่อชนิด
	{
		if (FishSummary == null) return;                      // ถ้าไม่มี Label ก็จบ

		if (GameProgress.FishCountByType == null              // ถ้า dictionary นับปลาไม่มีหรือว่าง
			|| GameProgress.FishCountByType.Count == 0)
		{
			FishSummary.Text = "0\t0\t0\t0";                  // แสดงศูนย์ทุกช่อง (แท็บคั่น)
			return;
		}

		string[] order = { "fish2", "fish3", "fish1", "shark" }; // ลำดับคอลัมน์ที่อยากแสดง
		var sb = new StringBuilder();                         // ใช้ StringBuilder ต่อสตริง
		foreach (var t in order)                              // วนตามลำดับชนิด
		{
			int c = GameProgress.FishCountByType.ContainsKey(t) // ถ้ามีคีย์นี้ใน dictionary
					? GameProgress.FishCountByType[t]           // ใช้ค่าจริง
					: 0;                                        // ถ้าไม่มีให้เป็น 0
			sb.Append(c).Append('\t');                          // ต่อจำนวน + แท็บคั่น
		}
		FishSummary.BbcodeEnabled = false;                   // ปิด BBCode เพื่อให้แสดงเป็นข้อความดิบ
		FishSummary.Text = sb.ToString().TrimEnd();          // เซ็ตข้อความ (ตัดแท็บท้าย)
	}

	private int LoadPersonalHigh(int levelIndex, string user) // โหลด high score ส่วนตัวของผู้เล่น/เลเวลนี้
	{
		var doc = LeaderboardStore.LoadDoc();                 // โหลดเอกสาร JSON (players + leaderboards)
		if (!doc.ContainsKey("players")) return 0;            // ถ้าไม่มีส่วน players → 0
		var players = (GDict)doc["players"];                  // อ้าง dict players
		if (!players.ContainsKey(user)) return 0;             // ถ้าไม่มีผู้ใช้นี้ → 0
		var p = (GDict)players[user];                          // อ็อบเจ็กต์ผู้ใช้
		if (!p.ContainsKey("levels")) return 0;               // ถ้าไม่มีข้อมูล levels → 0
		var lv = (GDict)p["levels"];                          // dict ระดับเลเวลทั้งหมดของผู้ใช้นี้
		var key = levelIndex.ToString();                      // แปลง index เป็นคีย์ string
		if (!lv.ContainsKey(key)) return 0;                   // ถ้ายังไม่เคยเล่นเลเวลนี้ → 0
		var obj = (GDict)lv[key];                             // อ็อบเจ็กต์ข้อมูลของเลเวลนี้
		return obj.ContainsKey("high")                        // ถ้ามีคีย์ high
			   ? (int)(long)obj["high"]                       // แปลงจาก long → int (ตามรูปแบบที่อ่านมาจาก JSON)
			   : 0;                                           // ไม่มีก็ 0
	}

	private string CurrentUserName()                          // คืนชื่อผู้ใช้ปัจจุบัน (fallback เป็น "Guest")
	{
		try
		{
			var n = PlayerLogin.Instance?.CurrentUser?.PlayerName; // พยายามดึงจากระบบล็อกอิน
			return string.IsNullOrEmpty(n) ? "Guest" : n;          // ถ้าว่าง/ไม่มี → "Guest"
		}
		catch { return "Guest"; }                                  // เผื่อมี exception ใด ๆ → "Guest"
	}

	public override void _UnhandledInput(InputEvent e)        // จัดการอินพุตที่ยังไม่ถูก node อื่นกินไป
	{
		if (e.IsActionPressed("ui_accept") || e.IsActionPressed("ui_cancel")) // ถ้ากด Enter/Space หรือ Cancel (ตาม mapping)
		{
			if (GameProgress.CurrentPlayingLevel == GameProgress.CurrentLevelIndex + 1)
				GameProgress.Advance();                       // ถ้าพึ่งเล่นจบเลเวลปัจจุบัน → ขยับดัชนีด่านไปขั้นถัดไป

			GetTree().ChangeSceneToFile("res://scenecheckpoint/checkpoint.tscn"); // กลับไปซีน checkpoint/เมนู
		}
	}
}
