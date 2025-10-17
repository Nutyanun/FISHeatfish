// res://scripts/HudCard.cs                     // ไฟล์สคริปต์ของ HUD (การ์ดแสดงผล)
using Godot;                                    // ใช้คลาสจากเอนจิน Godot (CanvasLayer, Label, Control, ฯลฯ)
using System;                                   // เนมสเปซมาตรฐานของ C# (ชนิดข้อมูล/ยูทิล)

// คลาส HUD ที่ลอยทับฉากหลัก
public partial class HudCard : CanvasLayer
{
	// ===== ScoreManager =====
	[Export] public NodePath ScoreManagerPath { get; set; } = null; // path ไปหา ScoreManager (แก้ได้ใน Inspector)
	private ScoreManager _sm;                                        // อ้างอิง ScoreManager ที่ใช้ส่งสัญญาณ/ซิงก์ค่า

	// === Navigation options ===
	[Export] public bool RetryReloadsScene { get; set; } = true;     // ปุ่ม Retry จะรีโหลดฉากเดิมไหม (true = reload)
	[Export(PropertyHint.File, "*.tscn")] public string MenuScenePath { get; set; } = "";  // พาธซีนเมนูหลัก (ถ้าอยากกลับเมนู)
	[Export(PropertyHint.File, "*.tscn")] public string NextScenePath { get; set; } = "";  // พาธซีนถัดไป (ถ้าใช้ Next)
	[Export] public bool QuitExitsGameIfNoMenu { get; set; } = true; // ถ้าไม่มีเมนูและกด Quit → ออกจากเกมเลยไหม

	// ===== Labels บนการ์ด =====
	[Export] public NodePath LevelLabelPath { get; set; } = "FreeLayer/LevelLabel"; // path label แสดงเลเวล
	[Export] public NodePath ScoreLabelPath { get; set; } = "FreeLayer/ScoreLabel"; // path label แสดงคะแนนรวม
	[Export] public NodePath NameLabelPath  { get; set; } = "FreeLayer/NameLabel";  // path label แสดงชื่อผู้เล่น
	[Export] public NodePath TimerLabelPath { get; set; } = "FreeLayer/TimerLabel"; // path label แสดงเวลา
	private Label _levelLabel, _scoreLabel, _nameLabel, _timerLabel;                // ตัวแปรเก็บอ้างอิง label ต่าง ๆ

	// ===== Fallback label (ถ้าไม่มี overlay container) =====
	[Export] public NodePath GameOverLabelPath { get; set; } = "GameOverLabel";     // path label กลางจอ ใช้กรณีไม่มี overlay
	private Label _gameOverLabel;                                                   // อ้างอิง label กลางจอ

	// ===== Overlay container =====
	[Export] public NodePath OverlayPath   { get; set; } = "GameOverLabel";         // path กล่อง overlay (หรือใช้ GameOverLabel แทน)
	[Export] public NodePath TitlePath     { get; set; } = "Center/root/Title";     // path label หัวข้อบน overlay
	[Export] public NodePath HintPath      { get; set; } = "Center/root/Hint";      // path label hint/คำแนะนำ
	[Export] public NodePath RetryPath     { get; set; } = "Center/root/Buttons/Retry"; // path ปุ่ม Retry
	[Export] public NodePath QuitPath      { get; set; } = "Center/root/Buttons/Quit";  // path ปุ่ม Quit
	[Export] public NodePath NextPath      { get; set; } = "Center/root/Buttons/Next";  // path ปุ่ม Next
	[Export] public NodePath ScoreGroup7Path { get; set; } = "ScoreGroup7";         // path กลุ่มโชว์คะแนน/ข้อมูลท้ายเกม

	private Control _overlay, _scoreGroup7;                                         // อ้างอิง overlay และกลุ่มคะแนน
	private Label _title, _hint;                                                    // อ้างอิง label หัวข้อและ hint
	private Button _retry, _quit, _next;                                            // อ้างอิงปุ่มต่าง ๆ

	// ===== Mult (ส้ม) 1..5 =====
	[Export] public NodePath[] MultFilledPaths { get; set; } = {                    // path ไอคอน mult ที่ "เติม" (สว่าง)
	"FreeLayer/MULT/F1","FreeLayer/MULT/F2","FreeLayer/MULT/F3","FreeLayer/MULT/F4","FreeLayer/MULT/F5"
	};
	[Export] public NodePath[] MultEmptyPaths  { get; set; } = {                    // path ไอคอน mult ที่ "ว่าง" (จาง)
	"FreeLayer/MULT/E1","FreeLayer/MULT/E2","FreeLayer/MULT/E3","FreeLayer/MULT/E4","FreeLayer/MULT/E5"
	};
	private CanvasItem[] _multFilled = new CanvasItem[5];                           // อ้างอิง node mult แบบเต็ม 5 ตำแหน่ง
	private CanvasItem[] _multEmpty  = new CanvasItem[5];                           // อ้างอิง node mult แบบว่าง 5 ตำแหน่ง

	// ===== Life (แดง) 0..3 =====
	[Export] public NodePath[] LifeFilledPaths { get; set; } = {                    // path รูปหัวใจ "เต็ม"
	"FreeLayer/Bgp/p/LF1","FreeLayer/Bgp/p/LF2","FreeLayer/Bgp/p/LF3"
	};
	[Export] public NodePath[] LifeEmptyPaths  { get; set; } = {                    // path รูปหัวใจ "ว่าง"
	"FreeLayer/Bgp/p/LE1","FreeLayer/Bgp/p/LE2","FreeLayer/Bgp/p/LE3"
	};
	private CanvasItem[] _lifeFilled = new CanvasItem[3];                           // อ้างอิงหัวใจเต็ม 3 ตำแหน่ง
	private CanvasItem[] _lifeEmpty  = new CanvasItem[3];                           // อ้างอิงหัวใจว่าง 3 ตำแหน่ง

	// ===== Flash score =====
	[Export] public Color ScoreFlashColor { get; set; } = new Color(1f, 0.9f, 0.2f); // สีแฟลชเวลา "ถึงเป้า" (โทนเหลืองทอง)
	[Export] public float ScoreFlashSeconds { get; set; } = 0.35f;                   // ระยะเวลาแฟลช (วินาที)
	private Color _scoreNormalColor = Colors.White;                                   // สีปกติของสกอร์
	private bool _flashedThisLevel = false;                                           // กันแฟลชซ้ำใน 1 เลเวล

	// ===== Crystal HUD =====
	[Export] public NodePath CrystalIconPath  { get; set; } = "FreeLayer/Crystal/Icon";   // path ไอคอนคริสตัล (ใน HUD)
	[Export] public NodePath CrystalCountPath { get; set; } = "FreeLayer/Crystal/Count";  // path label จำนวนคริสตัล
	private TextureRect _crystalIcon;                                                    // อ้างอิง TextureRect ไอคอนคริสตัล
	private Label _crystalCountLabel;                                                    // อ้างอิง label แสดง "xN"

	public override void _Ready()                                                        // เรียกเมื่อโหนดพร้อมใช้งาน
	{
	Layer = 100; // ให้อยู่บนสุด                                                         // จัดชั้นวาดสูง เพื่อให้ HUD ทับองค์ประกอบอื่น

	_sm = !ScoreManagerPath.IsEmpty ? GetNodeOrNull<ScoreManager>(ScoreManagerPath)     // หา ScoreManager ตาม path ถ้าตั้งไว้
	: GetNodeOrNull<ScoreManager>("%ScoreManager");                                // ไม่งั้นหาแบบ unique name %ScoreManager
	if (_sm == null) { GD.PushError("[HudCard] ScoreManager not found"); return; }      // หากหาไม่เจอ ให้ log error และหยุด

	_levelLabel    = GetNodeOrNull<Label>(LevelLabelPath);                              // ดึง label เลเวล
	_scoreLabel    = GetNodeOrNull<Label>(ScoreLabelPath);                              // ดึง label สกอร์รวม
	_nameLabel     = GetNodeOrNull<Label>(NameLabelPath);                               // ดึง label ชื่อผู้เล่น
	_timerLabel    = GetNodeOrNull<Label>(TimerLabelPath);                              // ดึง label เวลา
	_gameOverLabel = GetNodeOrNull<Label>(GameOverLabelPath);                           // ดึง label กลางจอ
	if (_gameOverLabel != null) _gameOverLabel.Visible = false;                         // ซ่อน label กลางจอไว้ก่อน
	if (_scoreLabel != null) _scoreNormalColor = _scoreLabel.Modulate;                  // เก็บค่าสีปกติของ score label

	for (int i = 0; i < 5; i++) {                                                      // วนผูก mult 5 ช่อง
	_multFilled[i] = GetNodeOrNull<CanvasItem>(i < MultFilledPaths.Length ? MultFilledPaths[i] : default); // node mult แบบเต็ม
	_multEmpty[i]  = GetNodeOrNull<CanvasItem>(i < MultEmptyPaths.Length  ? MultEmptyPaths[i]  : default); // node mult แบบว่าง
	}
	for (int i = 0; i < 3; i++) {                                                      // วนผูก life 3 ช่อง
	_lifeFilled[i] = GetNodeOrNull<CanvasItem>(i < LifeFilledPaths.Length ? LifeFilledPaths[i] : default); // node หัวใจเต็ม
	_lifeEmpty[i]  = GetNodeOrNull<CanvasItem>(i < LifeEmptyPaths.Length  ? LifeEmptyPaths[i]  : default); // node หัวใจว่าง
	}

	// ===== Overlay wiring =====
	_overlay = GetNodeOrNull<Control>(OverlayPath) ?? GetNodeOrNull<Control>("%GameOverLabel"); // หา overlay container (หรือใช้ %GameOverLabel)
	if (_overlay != null)                                                                      // ถ้ามี overlay ให้เดินต่อ
	{
	_title = _overlay.GetNodeOrNull<Label>(TitlePath)                                      // หา label title
	?? _overlay.GetNodeOrNull<Label>("%Title")                                    // หรือหาแบบ unique name
	?? _overlay.FindChild("Title", true, false) as Label;                         // หรือค้นเชิงลึกด้วยชื่อ

	_hint  = _overlay.GetNodeOrNull<Label>(HintPath)                                       // หา label hint
	?? _overlay.GetNodeOrNull<Label>("%Hint")
	?? _overlay.FindChild("Hint", true, false) as Label;

	_retry = _overlay.GetNodeOrNull<Button>(RetryPath)                                     // หา Button Retry
	?? _overlay.GetNodeOrNull<Button>("%Retry")
	?? _overlay.FindChild("Retry", true, false) as Button;

	_quit  = _overlay.GetNodeOrNull<Button>(QuitPath)                                      // หา Button Quit
	?? _overlay.GetNodeOrNull<Button>("%Quit")
	?? _overlay.FindChild("Quit", true, false) as Button;

	_next  = _overlay.GetNodeOrNull<Button>(NextPath)                                      // หา Button Next
	?? _overlay.GetNodeOrNull<Button>("%Next")
	?? _overlay.FindChild("Next", true, false) as Button;

	_scoreGroup7 = GetNodeOrNull<Control>(ScoreGroup7Path)                                 // หา score group พิเศษ (เช่นแสดงสรุป)
	  ?? GetNodeOrNull<Control>("%ScoreGroup7")
	  ?? _overlay.FindChild("ScoreGroup7", true, false) as Control;

	_overlay.ProcessMode = Node.ProcessModeEnum.Always;                                    // ให้ overlay ประมวลผลเสมอ (รองรับ input หลังจบเกม)
	_overlay.ZIndex = 1000;                                                                // ซ้อนบนสุด
	_overlay.Visible = false;                                                              // เริ่มต้นซ่อน
	_overlay.MouseFilter = Control.MouseFilterEnum.Ignore;                                 // ตอนซ่อนให้ input ทะลุไปได้
	if (_scoreGroup7 != null) _scoreGroup7.Visible = false;                                // ซ่อนกลุ่มคะแนน

	if (_retry != null) { _retry.ProcessMode = Node.ProcessModeEnum.Always; _retry.MouseFilter = Control.MouseFilterEnum.Stop; _retry.Pressed += OnRetryPressed; } // ปุ่ม Retry รับ input และผูก event
	if (_quit  != null) { _quit.ProcessMode  = Node.ProcessModeEnum.Always; _quit.MouseFilter  = Control.MouseFilterEnum.Stop;  _quit.Pressed  += OnQuitPressed;  } // ปุ่ม Quit
	if (_next  != null) { _next.ProcessMode  = Node.ProcessModeEnum.Always; _next.MouseFilter  = Control.MouseFilterEnum.Stop;  _next.Pressed  += OnNextPressed;  } // ปุ่ม Next

	GD.Print($"[HUD] overlay wired: retry={_retry!=null}, next={_next!=null}, quit={_quit!=null}"); // ล็อกสถานะปุ่ม
	}

	// signals
	_sm.ScoreChanged += OnScoreChanged;                // ต่อสัญญาณคะแนนด่านเปลี่ยน
	_sm.TotalScoreChanged += OnTotalScoreChanged;      // ต่อสัญญาณคะแนนรวมเปลี่ยน
	_sm.LivesChanged += OnLivesChanged;                // ต่อสัญญาณจำนวนชีวิตเปลี่ยน
	_sm.LevelChanged += OnLevelChanged;                // ต่อสัญญาณเลเวลเปลี่ยน
	_sm.MultiplierChanged += OnMultiplierChanged;      // ต่อสัญญาณตัวคูณคะแนนเปลี่ยน
	_sm.TimeLeftChanged += OnTimeLeftChanged;          // ต่อสัญญาณเวลาเหลือเปลี่ยน
	_sm.LevelCleared += OnLevelCleared;                // ต่อสัญญาณเคลียร์เลเวล
	_sm.GameOver += OnGameOver;                        // ต่อสัญญาณเกมโอเวอร์

	// ขอ sync ค่าเริ่มจาก ScoreManager (ต้องมีเมธอดนี้ใน ScoreManager)
	_sm.SyncRequestFromHud();                          // เรียกให้ ScoreManager ส่งค่าปัจจุบันกลับมา (ซิงก์ HUD ตอนเริ่ม)

	// ==== Crystal HUD wiring ====
	_crystalIcon       = GetNodeOrNull<TextureRect>(CrystalIconPath); // หาไอคอนคริสตัลใน HUD
	_crystalCountLabel = GetNodeOrNull<Label>(CrystalCountPath);      // หา label นับจำนวนคริสตัล

	if (_crystalCountLabel != null)                                         // ถ้ามี label จำนวน
	{
	_crystalCountLabel.Text = "x0";                                     // เริ่มด้วย x0
	_crystalCountLabel.Position += new Vector2(0, -10); // ยกขึ้น 10 px // ปรับตำแหน่งขึ้นเล็กน้อย (สวยงาม)
	}

	// === Show player name (ปลอดภัยเมื่อ PlayerLogin.Instance เป็น null) ===
	string playerName = PlayerLogin.Instance?.CurrentPlayerName ?? "Guest"; // ดึงชื่อผู้เล่นจากซิงเกิลตัน (ถ้าไม่มีใช้ Guest)
	if (_nameLabel != null) _nameLabel.Text = playerName;                   // อัปเดต label ชื่อ
	else GD.PushWarning("[HUD] NameLabel not found, cannot show player name."); // เตือนถ้าไม่มี label ชื่อ
	}

	public override void _UnhandledInput(InputEvent e)                     // รับอินพุตระดับบนสุดที่ยังไม่มีใครจัดการ
	{
	// ถ้า overlay ยังไม่ขึ้น → ไม่รับ input
	if (_overlay == null || !_overlay.Visible)                         // ต้องมี overlay และมองเห็น
	return;                                                        // ไม่งั้นไม่ต้องสนใจอินพุต

	// ป้องกัน pause ซ้ำจากระบบอื่น (PauseUI)
	GetViewport().SetInputAsHandled();  // บอกว่า input นี้ถูกจัดการแล้ว (จะไม่ส่งต่อให้ node อื่น) // กันชนกับ PauseUI

	// ตรวจปุ่ม Enter
	if (e.IsActionPressed("ui_accept"))                                // ถ้ากด Enter/Space (ui_accept)
	{
	if (_next != null && _next.Visible && !_next.Disabled)         // ถ้ามีปุ่ม Next และกดได้
	{
	OnNextPressed();                                           // ไปต่อ
	}
	else if (_retry != null && _retry.Visible && !_retry.Disabled) // ไม่งั้นถ้ามีปุ่ม Retry
	{
	OnRetryPressed();                                          // ลองใหม่ (รีโหลดฉาก)
	}
	}

	// ตรวจปุ่มออก
	if (e.IsActionPressed("ui_cancel") || e.IsActionPressed("quit"))   // ถ้ากด ESC หรือกด action ชื่อ quit
	{
	OnQuitPressed();                                               // เรียกออก/กลับเมนู
	}
	}

	// ===== Handlers =====
	private void OnLevelChanged(int level)                                  // เมื่อเลเวลเปลี่ยน
	{
	_flashedThisLevel = false;                                          // รีเซ็ตสถานะแฟลชของเลเวลนี้
	if (_levelLabel != null) _levelLabel.Text = $"LV.{level}";          // อัปเดต label เลเวล
	HideOverlay();                                                       // ซ่อน overlay (ถ้ามี)
	if (_gameOverLabel != null) _gameOverLabel.Visible = false;         // ซ่อน label กลางจอ (กรณีเคยแสดง)
	}

	private void OnScoreChanged(int levelScore, int target)                  // เมื่อคะแนนเลเวลเปลี่ยน
	{
	if (!_flashedThisLevel && target > 0 && levelScore >= target) {     // ถ้ายังไม่แฟลช และถึง/เกินเป้าหมาย
	_flashedThisLevel = true;                                       // กันแฟลชซ้ำ
	FlashScoreOnce();                                               // แฟลชสีคะแนนครั้งเดียว
	}
	}

	private void OnTotalScoreChanged(int totalScore, int highScore)          // เมื่อคะแนนรวมเปลี่ยน
	{
	if (_scoreLabel != null) _scoreLabel.Text = totalScore.ToString();  // อัปเดต label คะแนนรวม
	}

	private void OnLivesChanged(int lives)                                   // เมื่อจำนวนชีวิตเปลี่ยน
	{
	int f = Mathf.Clamp(lives, 0, 3);                                   // บีบค่าให้อยู่ 0..3
	for (int i = 0; i < 3; i++) {                                       // อัปเดตหัวใจทั้ง 3 ช่อง
	if (_lifeEmpty[i]  != null) _lifeEmpty[i].Visible  = true;      // แสดงหัวใจว่างเป็นพื้น
	if (_lifeFilled[i] != null) _lifeFilled[i].Visible = (i < f);   // แสดงหัวใจเต็มตามจำนวนชีวิต
	if (_lifeFilled[i] != null && _lifeEmpty[i] != null) _lifeFilled[i].ZIndex = _lifeEmpty[i].ZIndex + 1; // ให้เต็มซ้อนหน้าว่าง
	}
	}

	private void OnMultiplierChanged(int mult, int fishInWindow, int needFish, float windowLeft) // เมื่อ mult เปลี่ยน
	{
	int f = Mathf.Clamp(mult, 0, 5);                                   // บีบค่า mult 0..5
	for (int i = 0; i < 5; i++) {                                       // อัปเดตแถบ mult ทั้ง 5 ช่อง
	if (_multEmpty[i]  != null) _multEmpty[i].Visible  = true;      // แสดงช่องว่างเป็นพื้น
	if (_multFilled[i] != null) _multFilled[i].Visible = (i < f);   // เติมช่องตามค่า mult
	if (_multFilled[i] != null && _multEmpty[i] != null) _multFilled[i].ZIndex = _multEmpty[i].ZIndex + 1; // ให้เต็มอยู่หน้า
	}
	}

	private void OnTimeLeftChanged(float timeLeft)                           // เมื่อเวลาเหลือเปลี่ยน
	{
	if (_timerLabel == null) return;                                    // ไม่มี label ก็ออก
	int t = Mathf.Max(0, Mathf.CeilToInt(timeLeft));                    // ปัดเศษขึ้นเป็นวินาที และไม่ต่ำกว่า 0
	_timerLabel.Text = $"Time : {t/60:00}:{t%60:00}";                   // แสดงผลเป็น MM:SS
	}

	private void OnGameOver(int finalScore, int level)                       // เมื่อเกมจบ (ชีวิตหมด/ไม่ถึงเป้า)
	{
	GD.Print("[HUD] OnGameOver received");                               // ล็อก
	if (_overlay != null)                                                // ถ้ามี overlay UI
	ShowOverlay(OverlayMode.GameOver, "GAME OVER", "Press ENTER or click Retry"); // แสดงโอเวอร์เลย์โหมด Game Over
	else
	ShowCenterBigLabel("GAME OVER");                                  // ไม่งั้นใช้ label กลางจอ
	}

	private void OnLevelCleared(int finalScore, int level)                   // เมื่อเคลียร์เลเวล
	{
	GD.Print("[HUD] OnLevelCleared received");                           // ล็อก
	if (_overlay != null)                                                // ถ้ามี overlay UI
	ShowOverlay(OverlayMode.LevelClear, $"LEVEL {level} CLEAR!", "Press ENTER or click Next"); // แสดงโหมดเคลียร์เลเวล
	else
	ShowCenterBigLabel($"LEVEL {level} CLEAR!");                      // ไม่งั้นใช้ label กลางจอ
	}

	// ===== Overlay helpers =====
	private enum OverlayMode { GameOver, LevelClear }                        // โหมดของ overlay: จบเกม / ผ่านด่าน

	private void ShowOverlay(OverlayMode mode, string title, string hint)    // โชว์ overlay พร้อมตั้งค่าปุ่ม/ข้อความ
	{
	if (_overlay == null) return;                                        // ไม่มี overlay → ออก

	if (_title != null) _title.Text = title;                             // ตั้งหัวข้อ
	if (_hint  != null) _hint.Text  = hint;                              // ตั้งคำแนะนำ

	// โชว์/ซ่อนและ disable ปุ่มให้ถูกโหมด
	if (_retry != null) { _retry.Visible = (mode == OverlayMode.GameOver); _retry.Disabled = !_retry.Visible; } // Retry เฉพาะ GameOver
	if (_next  != null) { _next.Visible  = (mode == OverlayMode.LevelClear); _next.Disabled  = !_next.Visible; } // Next เฉพาะ LevelClear
	if (_quit  != null) { _quit.Visible  = true; _quit.Disabled = false; }                                       // Quit แสดงเสมอ

	_overlay.Visible = true;                                             // แสดง overlay
	_overlay.ZIndex = 1000;                                              // ซ้อนบนสุด
	_overlay.ProcessMode = Node.ProcessModeEnum.Always;                  // ให้ทำงานแม้ pause
	_overlay.MouseFilter = Control.MouseFilterEnum.Stop;                 // บล็อกคลิกทะลุ

	if (_scoreGroup7 != null) _scoreGroup7.Visible = true;               // โชว์กลุ่มคะแนน (ถ้ามี)

	// โฟกัสปุ่มแรกที่มองเห็น
	if (_next != null && _next.Visible) _next.GrabFocus();               // ถ้ามี Next ให้โฟกัส
	else if (_retry != null && _retry.Visible) _retry.GrabFocus();       // ไม่งั้นโฟกัส Retry
	else if (_quit != null && _quit.Visible) _quit.GrabFocus();          // สุดท้ายโฟกัส Quit
	}

	private void HideOverlay()                                               // ซ่อน overlay และกลุ่มคะแนน
	{
	if (_overlay == null) return;                                        // ไม่มี overlay → ออก
	_overlay.Visible = false;                                            // ซ่อน overlay
	_overlay.MouseFilter = Control.MouseFilterEnum.Ignore;               // ให้ input ทะลุได้เมื่อซ่อน
	if (_scoreGroup7 != null) _scoreGroup7.Visible = false;              // ซ่อนกลุ่มคะแนน
	}

	private async void ShowCenterBigLabel(string text)                       // แสดง label กลางจอเป็น fallback
	{
	if (_gameOverLabel == null) return;                                  // ไม่มี label → ออก

	_gameOverLabel.Visible = true;                                       // โชว์ label
	_gameOverLabel.Text = text;                                          // ตั้งข้อความ
	_gameOverLabel.ZIndex = 9999;                                        // ซ้อนบนสุดมาก ๆ
	_gameOverLabel.Modulate = new Color(1,1,1,0);                        // เริ่มโปร่งใส (สำหรับเฟดอิน)

	await ToSignal(GetTree(), "process_frame");                          // รอ 1 เฟรมให้ layout อัปเดตก่อน
	var vp = GetViewport().GetVisibleRect().Size;                        // ขนาดหน้าจอ
	var sz = _gameOverLabel.GetRect().Size;                              // ขนาด label
	_gameOverLabel.Position = (vp - sz) * 0.5f;                          // จัดให้อยู่กลางจอ

	var tween = CreateTween();                                           // สร้างทวีน
	tween.TweenProperty(_gameOverLabel, "modulate:a", 1.0, 0.35);        // เฟดจากโปร่งใส → ทึบใน 0.35 วินาที
	GD.Print($"[HUD] show label '{text}' at {_gameOverLabel.Position}, size={sz}"); // ล็อกตำแหน่ง/ขนาด
	}

	// ===== Buttons =====
	private async void OnRetryPressed()                                      // เมื่อกดปุ่ม Retry
	{
	GD.Print("[HUD] Retry pressed");                                     // ล็อก

	// ปลด pause ก่อน reload (สำคัญมาก!)
	GetTree().Paused = false;                                            // ปลดหยุดเกม

	// ถ้ามี overlay ก็ซ่อน
	HideOverlay();                                                       // ซ่อน overlay เพื่อไม่ให้ค้าง

	// รอให้ process frame นึง เพื่อให้ tree resume แล้วค่อย reload
	await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);        // รอ 1 เฟรม

	GetTree().ReloadCurrentScene();                                      // รีโหลดฉากเดิม
	}

	private async void OnNextPressed()                                       // เมื่อกดปุ่ม Next
	{
	GD.Print("[HUD] Next pressed");                                      // ล็อก

	GetTree().Paused = false;                                            // ปลด pause
	HideOverlay();                                                       // ซ่อน overlay
	await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);        // รอ 1 เฟรม
	GetTree().ChangeSceneToFile("res://scenescore/score.tscn");          // ไปหน้าสรุปคะแนน (ตามโปรเจ็กต์นี้)
	}

	private void OnQuitPressed()                                             // เมื่อกดปุ่ม Quit
	{
	GD.Print("[HUD] Quit pressed");                                      // ล็อก

	if (!string.IsNullOrEmpty(MenuScenePath))                            // ถ้าตั้งหน้าเมนูไว้
	{
	// ถ้าตั้งหน้าเมนูไว้ ให้กลับไปเมนูแทนการออก
	GetTree().Paused = false;                                        // ปลด pause
	GetTree().ChangeSceneToFile(MenuScenePath);                      // ไปหน้าเมนู
	return;                                                          // จบ
	}

	if (QuitExitsGameIfNoMenu)                                          // ถ้าไม่มีเมนูและอนุญาตให้ออกเกม
	{
	QuitApp();   // ออกจากเกมแบบกันพลาด/ข้ามแพลตฟอร์ม
	}
	else
	{
	HideOverlay(); // ไม่ออกก็ซ่อนโอเวอร์เลย์
	}
	}

	// --- helper: ออกจากเกมแบบครอบคลุม ---
	private void QuitApp()                                                   // ช่วยออกจากเกม (รองรับหลายแพลตฟอร์ม)
	{
	// บางแพลตฟอร์ม/บางจังหวะสั่ง Quit ตรง ๆ อาจไม่ทำงาน ให้ลองทั้งทันทีและแบบ deferred
	GetTree().Paused = false;                                            // ปลด pause ให้แน่ใจก่อน

	// เคสเว็บ (HTML5) ส่วนใหญ่ "ออก" จะทำอะไรไม่ได้ ให้ลองกลับหน้าเมนูถ้ามี
	if (OS.HasFeature("web"))                                            // หากรันบนเว็บ
	{
	GD.Print("[HUD] Quit on web: fallback to hide or go menu if set."); // ล็อก
	if (!string.IsNullOrEmpty(MenuScenePath))                         // ถ้ามีพาธเมนู
	{
	GetTree().ChangeSceneToFile(MenuScenePath);                   // ไปหน้าเมนูแทน
	}
	else
	{
	// บนเว็บไม่มีการปิดหน้าต่างจากเกมได้ ปิดโอเวอร์เลย์แทน
	HideOverlay();                                               // ซ่อน overlay
	}
	return;                                                          // จบฟังก์ชัน
	}

	// ลองแบบปกติ
	GetTree().Quit();                                                    // สั่งออกจากเกม

	// เผื่อสั่งในสัญญาณปุ่มแล้วไม่ปิด ให้ deferred อีกครั้ง
	Callable.From(() => GetTree().Quit()).CallDeferred();                // สั่งปิดแบบ deferred

	// เผื่อกรณีขี้เกียจจริง ๆ: ส่ง notification ปิดหน้าต่าง (desktop)
	if (!OS.HasFeature("web"))                                           // ถ้าไม่ใช่เว็บ (เดสก์ท็อป)
	{
	GetTree().Root.PropagateNotification((int)Node.NotificationWMCloseRequest); // ส่งแจ้งปิดหน้าต่าง
	}
	}

	private void OnResetHighPressed()                                        // ปุ่มรีเซ็ต high score (ยังไม่ได้ทำจริง)
	{
	GD.Print("[HUD] ResetHigh pressed (implement if needed)");           // เตือนว่าต้อง implement เองถ้าต้องใช้
	}

	// ===== Flash score =====
	private async void FlashScoreOnce()                                      // แฟลชสีคะแนน 1 ครั้งเมื่อถึงเป้า
	{
	if (_scoreLabel == null) return;                                     // ไม่มี label → ออก
	_scoreLabel.Modulate = ScoreFlashColor;                              // เปลี่ยนสีเป็นสีแฟลช
	await ToSignal(GetTree().CreateTimer(ScoreFlashSeconds), "timeout"); // รอเวลาที่กำหนด
	_scoreLabel.Modulate = _scoreNormalColor;                            // กลับเป็นสีเดิม
	}

	public override void _ExitTree()                                            // เรียกตอนโหนดกำลังออกจากซีน/ถูกทำลาย
	{
	if (_sm != null);                                                    // (ไม่มีการ unsubscribe สัญญาณในโค้ดนี้; เครื่องหมาย ; ทำให้ไม่มีผลเพิ่ม)
	}
}
