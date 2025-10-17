using Godot;                                               // ใช้คลาส/ระบบของเอนจิน Godot (Node, Node2D, Signals, FileAccess ฯลฯ)
using System;                                              // เนมสเปซมาตรฐานของ C# (ชนิดข้อมูล/ยูทิล)
using System.Collections.Generic;                          // คอลเลกชันทั่วไปของ .NET (ถ้าจะใช้ List/Dictionary ฝั่ง .NET)
using GDict = Godot.Collections.Dictionary;                // ตั้งชื่อย่อให้ Godot.Collections.Dictionary เป็น GDict (หากต้องการใช้ดิกของ Godot)

public partial class ScoreManager : Node2D                 // ผู้จัดการสกอร์/เวลา/เลเวล หลักของเกม (อยู่ในซีนเป็น Node2D)
{
public static ScoreManager Instance { get; private set; } // ซิงเกิลตันแบบง่าย: ให้เข้าถึง ScoreManager ปัจจุบันได้จากที่อื่น

public override void _EnterTree()                           // เรียกก่อน _Ready() เสมอ (เหมาะกับการตั้งค่า Autoload/ซิงเกิลตัน)
{
// จะถูกเรียกก่อน _Ready() เสมอ → เหมาะสำหรับ Autoload
Instance = this;                                        // ตั้งอินสแตนซ์ซิงเกิลตัน
GD.Print("[ScoreManager] Autoload instance ready.");    // พิมพ์ดีบักว่าพร้อมใช้งานแล้ว
}

// ===== Signals =====
[Signal] public delegate void ScoreChangedEventHandler(int levelScore, int target);            // แจ้งคะแนนของด่าน + เป้าหมาย เปลี่ยน
[Signal] public delegate void TotalScoreChangedEventHandler(int totalScore, int highScore);    // แจ้งคะแนนรวม + HighScore เปลี่ยน
[Signal] public delegate void LivesChangedEventHandler(int lives);                             // แจ้งจำนวนชีวิตเปลี่ยน
[Signal] public delegate void LevelChangedEventHandler(int level);                             // แจ้งเลเวลเปลี่ยน
[Signal] public delegate void MultiplierChangedEventHandler(int mult, int fishInWindow, int needFish, float windowLeft); // แจ้งตัวคูณ/สถานะคอมโบ
[Signal] public delegate void TimeLeftChangedEventHandler(float timeLeft);                     // แจ้งเวลาเหลือเปลี่ยน
[Signal] public delegate void LevelClearedEventHandler(int finalScore, int level);             // แจ้งผ่านด่าน (เวลาหมด + ถึงเป้า)
[Signal] public delegate void GameOverEventHandler(int finalScore, int level);                 // แจ้งเกมจบ (ชีวิตหมด หรือเวลาหมดแต่ไม่ถึงเป้า)

[Signal] public delegate void BonusScoreChangedEventHandler(int totalBonus);                   // แจ้งคะแนนโบนัสเปลี่ยน
[Signal] public delegate void BonusPhaseStartedEventHandler();                                 // แจ้งเริ่มช่วงโบนัส (ถ้าใช้)
[Signal] public delegate void BonusPhaseEndedEventHandler(int totalBonus);                     // แจ้งจบช่วงโบนัส

// ===== Config =====
[Export] public int  BaseTargetScore { get; set; } = 300;       // เป้าคะแนนฐานของเลเวล 1
[Export] public int  TargetIncrement { get; set; } = 1000;      // คะแนนเป้าเพิ่มต่อเลเวล
[Export] public int  StartLives      { get; set; } = 3;         // จำนวนชีวิตเริ่มต้น
[Export] public bool InfiniteLives   { get; set; } = false;     // โหมดชีวิตไม่จำกัด (ใช้ดีบัก/ทดสอบ)

// === Combo / Multiplier config ===
[Export] public int  ComboFishRequired = 10;   // ต้องกินปลาให้ครบกี่ตัวภายในหน้าต่างเวลา เพื่อเพิ่มตัวคูณ
[Export] public float ComboWindowSec   = 15f;  // ระยะเวลา (วินาที) ของหน้าต่างคอมโบในแต่ละรอบ

// ==== CRYSTAL SPAWNER HOOK ====
[Export] public NodePath CrystalSpawnerPath { get; set; } = null; // Path ให้ลากโหนด CrystalSpawner มาผูก
private CrystalSpawner _crys;     // อ้างอิง CrystalSpawner ผ่าน Path (ถ้าใช้วิธีนี้)
private bool _pinkForcedThisLevel; // กันการบังคับสปอว์นคริสตัลชมพูซ้ำภายในด่านเดียวกัน (ไม่ได้ใช้ต่อในโค้ดนี้ แต่เป็นธงสำรอง)

// ===== State =====
public int Level { get; private set; } = 1;     // เลเวลปัจจุบัน
public int TotalScore { get; private set; }     // คะแนนรวมทุกด่าน
public int LevelScore { get; private set; }     // คะแนนของด่านปัจจุบัน
public int TargetScore { get; private set; }    // เป้าหมายคะแนนของด่านปัจจุบัน

public float TimeLeftSec { get; private set; } = 90f; // เวลาเริ่มต้น (ดีฟอลต์ 90 วิ)
[Export] public bool CountDown = true;                 // นับเวลาถอยหลัง (true) หรือเดินหน้า (false)

private int _mult = 1;             // ตัวคูณคะแนนปัจจุบัน (เริ่มที่ 1)
private int _fishInWindow = 0;     // จำนวนปลาที่กินในหน้าต่างคอมโบรอบปัจจุบัน
private int _needFish = 3;         // (ดูเหมือนค่าตัวอย่าง/ดีบัก) ใช้บอก HUD ว่าต้องกินอีกกี่ตัว (ตั้งเท่ากับ ComboFishRequired ตอนรีเซ็ต)
private float _windowLeft = 0f;    // เวลาที่เหลือของหน้าต่างคอมโบ

private int _lives;                // จำนวนชีวิตปัจจุบัน
private int _highScore = 0;        // สถิติคะแนนรวมสูงสุด

private CrystalSpawner _crystal;   // อ้างอิง CrystalSpawner จากชื่อโหนดในซีน (อีกวิธี)

private BonusCoinSpawner _bonus;   // อ้างอิงสปอว์นเหรียญโบนัส (ถ้ามีในซีน)

private bool _coinScheduledLast20 = false; // กันสั่งเหรียญตกซ้ำใน 20 วิสุดท้าย
private bool _pinkScheduledLast20 = false; // กันสั่งชมพูซ้ำใน 20 วิสุดท้าย
private float _prevTimeLeft = 0f;         // เวลาเดิม (เอาไว้ตรวจขอบ 20 วิ)
private bool _bonusEnabledThisLevel = false; // ด่านนี้เปิดโหมดโบนัสหรือไม่

private bool _isRunning = true;    // เกมกำลังวิ่ง (true) หรือหยุดตรรกะ (false)
public  bool IsGameOver { get; private set; } // จบเกมแล้วหรือยัง

// ใช้คู่กับ GameProgress เพื่อไม่ให้นับด่านเกิน
public  bool IsLevelCleared { get; private set; } // ผ่านด่านแล้วหรือยัง

// Bonus (เก็บตัวเลขไว้ให้ HUD)
private int  _bonusScore = 0;      // คะแนนโบนัสสะสมของด่าน (ให้ HUD อ่าน)

private const string SAVE_FILE = "user://save.dat"; // ที่เก็บ high score แบบง่ายในโฟลเดอร์ผู้ใช้ของเกม

public void AddTime(double s){ TimeLeftSec += (float)s; if(CountDown && TimeLeftSec<0) TimeLeftSec=0; EmitSignal(SignalName.TimeLeftChanged, TimeLeftSec); } // เพิ่มเวลา (รับค่าบวก/ลบ) แล้วส่งสัญญาณ
public void AddTimeSeconds(double s)=>AddTime(s);     // ชื่อช่วยจำ: เพิ่มเวลาเป็นวินาที
public void AddBonusTime(double s)=>AddTime(s);       // ชื่อช่วยจำ: เพิ่มเวลาแบบโบนัส
public void ReduceTime(double s)=>AddTime(-Math.Abs(s));   // ลดเวลา (เป็นค่าลบ)
public void SubtractTime(double s)=>AddTime(-Math.Abs(s)); // ลดเวลา (สำเนาชื่อฟังก์ชัน)

// ===== กติกาด่าน =====
private readonly struct LevelRule   // โครงสร้างกำหนด “สูตรของด่าน”
{
public readonly int Seconds;                 // ระยะเวลา (วินาที) ของด่าน
public readonly bool BonusOn;                // เปิดช่วงโบนัส (เหรียญ/กิจกรรมพิเศษ) ในด่านนี้ไหม
public readonly CrystalType[] CrystalColors; // สีคริสตัลที่อนุญาตให้เกิดในด่านนี้
public readonly float CrystalIntervalSec;    // ช่วงเวลาการเกิดคริสตัล
public readonly int   CrystalMaxOnScreen;    // จำนวนคริสตัลสูงสุดบนจอพร้อมกัน

public LevelRule(int seconds, bool bonusOn, CrystalType[] colors, float intervalSec, int maxOnScreen)
{
Seconds = seconds;                       // เซ็ตค่าเวลา
BonusOn = bonusOn;                       // เซ็ตเปิด/ปิดโบนัส
CrystalColors = colors;                  // เซ็ตชุดสีคริสตัล
CrystalIntervalSec = intervalSec;        // เซ็ตอินเทอร์วอล
CrystalMaxOnScreen = maxOnScreen;        // เซ็ตจำนวนสูงสุด
}
}

private static readonly CrystalType[] NONE = Array.Empty<CrystalType>(); // ค่าคงที่: ไม่มีคริสตัล

// ปรับตามสเปค:
// L1-L3 ไม่มีคริสตัล
// L4: Green+Blue (2 สี), L5: +Purple, L6: +Red, L7: +Pink
private readonly LevelRule[] _rules =   // ตารางกติกาของแต่ละเลเวล
{
new LevelRule(90, false, NONE, 45f, 1),                                                           // L1: 90 วิ, ไม่มีโบนัส/คริสตัล
new LevelRule(120, true,  NONE, 45f, 1),                                                          // L2: 120 วิ, เปิดโบนัส, ไม่มีคริสตัล
new LevelRule(150, true,  NONE, 45f, 1),                                                          // L3: 150 วิ, เปิดโบนัส, ไม่มีคริสตัล

new LevelRule(180, true,  new[]{ CrystalType.Green, CrystalType.Blue }, 45f, 2),                                               // L4: มี Green+Blue, สูงสุด 2 ชิ้น
new LevelRule(180, true,  new[]{ CrystalType.Green, CrystalType.Blue, CrystalType.Purple }, 45f, 3),                             // L5: เพิ่ม Purple, สูงสุด 3 ชิ้น
new LevelRule(180, true,  new[]{ CrystalType.Green, CrystalType.Blue, CrystalType.Purple, CrystalType.Red }, 45f, 4),           // L6: เพิ่ม Red, สูงสุด 4 ชิ้น
new LevelRule(180, true,  new[]{ CrystalType.Green, CrystalType.Blue, CrystalType.Purple, CrystalType.Red, CrystalType.Pink }, 45f, 5), // L7: เพิ่ม Pink, สูงสุด 5 ชิ้น
};

// ===== INITIALIZE =====
public override void _Ready()                                // เรียกเมื่อโหนดพร้อมทำงาน
{
LoadHighScore();                                         // โหลดสถิติ HighScore
Level = (GameProgress.CurrentPlayingLevel > 0) ? GameProgress.CurrentPlayingLevel : 1; // กำหนดเลเวลเริ่ม (จาก GameProgress ถ้ามี)

_crystal = GetNodeOrNull<CrystalSpawner>("%CrystalSpawner") ?? GetNodeOrNull<CrystalSpawner>("CrystalSpawner"); // หา CrystalSpawner จากซีน (unique name ก่อน)

_bonus = GetTree().CurrentScene?.FindChild("BonusCoinSpawner", true, false) as BonusCoinSpawner; // หา BonusCoinSpawner แบบค้นเชิงลึก

if (CrystalSpawnerPath != null && !CrystalSpawnerPath.IsEmpty)  // ถ้าตั้ง Path ไว้ใน Inspector
_crys = GetNode<CrystalSpawner>(CrystalSpawnerPath);            // ใช้ Path เพื่อหา CrystalSpawner
else
_crys = GetTree().CurrentScene?.FindChild("CrystalSpawner", true, false) as CrystalSpawner; // ไม่งั้นลองค้นเชิงลึกด้วยชื่อ

_lives = InfiniteLives ? int.MaxValue / 2 : StartLives;  // ตั้งจำนวนชีวิตเริ่มต้น (ถ้า infinite ให้ค่าสูง ๆ)
EmitSignal(SignalName.LivesChanged, _lives);             // กระจายสัญญาณแจ้ง HUD

StartLevel(Level);                                       // เริ่มด่านปัจจุบัน
}

private LevelRule GetRule(int lv)                           // คืนกติกาของด่าน (ป้องกัน index เกิน)
{
if (lv <= 0) lv = 1;
if (lv > _rules.Length) lv = _rules.Length;
return _rules[lv - 1];
}

private void StartLevel(int nextLevel)                      // ตั้งค่าทั้งหมดเพื่อเริ่มด่านใหม่
{
Level = Math.Max(1, nextLevel);                         // บังคับไม่ให้ต่ำกว่า 1
var rule = GetRule(Level);                               // ดึงกติกาของด่านนี้

TimeLeftSec = rule.Seconds;                              // ตั้งเวลาเริ่มต้นของด่าน
TargetScore = BaseTargetScore + (Level - 1) * TargetIncrement; // คำนวณเป้าคะแนนของด่าน
LevelScore  = 0;                                         // รีเซ็ตคะแนนด่าน

EmitSignal(SignalName.TimeLeftChanged, TimeLeftSec);     // แจ้ง HUD
EmitSignal(SignalName.ScoreChanged, LevelScore, TargetScore);

_bonusScore = 0;                                         // รีเซ็ตโบนัสด่านนี้
_bonusEnabledThisLevel = rule.BonusOn;                   // ด่านนี้เปิดโบนัสหรือไม่
// รีเซ็ตธงช่วง 20 วิท้าย + กันเศษโบนัสค้างจากด่านก่อน
_coinScheduledLast20 = false;                            // ยังไม่สั่งเหรียญตกในช่วงท้าย
_pinkScheduledLast20 = false;                            // ยังไม่สั่งชมพูในช่วงท้าย
_prevTimeLeft = TimeLeftSec;                             // เวลาเดิมตั้งต้นเท่ากับเต็มด่าน
_bonus?.ForceStopAndClear();                             // ถ้ามีโบนัสค้าง ให้หยุดและเคลียร์ทิ้ง

_mult = 1; _fishInWindow = 0; _windowLeft = 0f;          // รีเซ็ตสถานะคอมโบ/ตัวคูณ

IsLevelCleared = false;                                  // ยังไม่ผ่านด่าน
IsGameOver = false;                                      // ยังไม่จบเกม
GameProgress.IsLevelCleared = false;  //  รีเซ็ตทุกครั้งที่เริ่มด่าน // ซิงค์สถานะให้ระบบติดตามความคืบหน้า
_isRunning = true;                                       // ให้ตรรกะเกมทำงาน
// ใช้ Level (ตัวใหญ่) และไม่ประกาศชื่อ rule ซ้ำ
if (Level >= 4 && _crystal != null)                             // ตั้งค่า CrystalSpawner เฉพาะด่านที่มีคริสตัล
{
var lvRule = _rules[Level - 1];                              // ดึงกติกาของเลเวลนี้
// ไม่ต้อง MapColors อีกแล้ว ส่งตรง ๆ
_crystal.ApplyRule(lvRule.CrystalColors, lvRule.CrystalIntervalSec, lvRule.CrystalMaxOnScreen); // ส่งสี/ช่วงเวลา/จำนวนสูงสุดให้สปอว์นเนอร์
_crystal.ResetPinkForced();                                  // รีเซ็ตธงบังคับชมพู (ให้มีสิทธิ์บังคับอีกครั้งใน 20 วิท้าย)
}

EmitSignal(SignalName.LevelChanged, Level);              // แจ้ง HUD ว่าเลเวลเปลี่ยน
_pinkForcedThisLevel = false;                           // รีเซ็ตธงชมพู (สำรอง)

}

public override void _Process(double delta)                  // วนทำงานทุกเฟรม (ตรรกะเวลา/ช่วงท้าย/คอมโบ)
{

if (!_isRunning || IsGameOver || IsLevelCleared) return; // ถ้าเกมหยุด/จบ/ผ่านแล้ว ไม่ต้องทำต่อ
float dt = (float)delta;                                  // แคสต์ delta เป็น float

if (CountDown)                                            // โหมดนับถอยหลัง
{
TimeLeftSec -= dt;                                    // ลดเวลา
if (TimeLeftSec < 0f) TimeLeftSec = 0f;               // ไม่ให้ติดลบ
}
else                                                      // โหมดนับเดินหน้า
{
TimeLeftSec += dt;                                    // เพิ่มเวลา
}

EmitSignal(SignalName.TimeLeftChanged, TimeLeftSec);      // แจ้ง HUD ว่าเวลาเปลี่ยน

// === Trigger 20 วินาทีสุดท้าย ===
bool justEnteredLast20 = CountDown && (_prevTimeLeft > 20f) && (TimeLeftSec <= 20f); // เพิ่ง “ข้ามเข้า” โซน 20 วิท้ายหรือไม่
if (justEnteredLast20)
{
// 1) coins ตกเฉพาะ 20 วิท้าย เฉพาะด่านที่เปิดโบนัส
if (_bonusEnabledThisLevel && _bonus != null && !_coinScheduledLast20) // ถ้าเปิดโบนัสและยังไม่สั่งเหรียญ
{
int duration = Mathf.CeilToInt(Mathf.Max(0f, TimeLeftSec)); // คำนวณเวลาที่เหลือ (ปัดขึ้น)
if (duration > 0)
{
_bonus.ApplyLevelTuning(duration: duration);               // ปรับจูนสปอว์นเหรียญให้เท่าระยะเวลาที่เหลือ
_bonus.Start(duration);                                     // เริ่มปล่อยเหรียญ
_coinScheduledLast20 = true;                                // กันสั่งซ้ำ
}
}

// 2) (ถ้ามีเพชร L4+) บังคับชมพู 1 ชิ้น
if (_crystal != null && !_pinkScheduledLast20 && Level >= 4)   // ในเลเวลที่มีระบบคริสตัล
{
_crystal.ForcePinkOnceInLastWindow();                       // บังคับเกิดชมพู 1 ชิ้นในหน้าต่างสุดท้าย
_pinkScheduledLast20 = true;                                // กันสั่งซ้ำ
}
}
_prevTimeLeft = TimeLeftSec;                                // อัปเดตเวลาเดิมสำหรับรอบถัดไป
if (CountDown && TimeLeftSec <= 0f)                                       // หมดเวลาแล้ว (ในโหมดนับถอยหลัง)
{
if (LevelScore >= TargetScore)                                 // ถ้าคะแนนถึงเป้า → ผ่านด่าน
{
_bonus?.StopNow();                                         // หยุดโบนัสทันที
OnLevelCleared();                                          // เรียกผ่านด่าน
}
else                                                           // ไม่ถึงเป้า → จบเกม
{
_bonus?.StopNow();
OnGameOver();
}
}

if (_windowLeft > 0f)                                             // ถ้ายังอยู่ในหน้าต่างคอมโบ
{
_windowLeft -= dt;                                            // ลดเวลาของหน้าต่าง
if (_windowLeft <= 0f)                                        // หมดเวลา
{
_mult = 1; _fishInWindow = 0; _windowLeft = 0f;           // รีเซ็ตตัวคูณและตัวนับ
EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, _needFish, _windowLeft); // แจ้ง HUD
}
}
}

// ===== SCORE SYSTEM =====
public void AddScore(int baseScore)                                     // เพิ่มคะแนนเมื่อ “กินปลา” หรือได้สกอร์จากอื่น ๆ
{
if (IsGameOver || IsLevelCleared) return;                           // ถ้าจบ/ผ่านแล้วไม่คิดคะแนนเพิ่ม
int add = Math.Max(0, baseScore) * Math.Max(1, _mult);              // ป้องกัน baseScore ลบ และใช้ตัวคูณอย่างน้อย 1
LevelScore += add;                                                  // เพิ่มคะแนนด่าน
TotalScore += add;                                                  // เพิ่มคะแนนรวม

if (TotalScore > _highScore)                                        // อัปเดต HighScore ถ้าทำลายสถิติ
{
_highScore = TotalScore;
SaveHighScore();                                               // บันทึกลงไฟล์
}

EmitSignal(SignalName.ScoreChanged, LevelScore, TargetScore);       // แจ้ง HUD คะแนนด่าน
EmitSignal(SignalName.TotalScoreChanged, TotalScore, _highScore);   // แจ้ง HUD คะแนนรวม/ไฮสกอร์

// === Combo / Multiplier ===
// เริ่มหน้าต่างคอมโบเมื่อกินตัวแรก หรือถ้ายังมีหน้าต่างอยู่ก็ขยายให้อยู่ที่ 20 วิเสมอ
if (_windowLeft <= 0f)                                              // ถ้าไม่มีหน้าต่างคอมโบอยู่
{
_windowLeft = ComboWindowSec;                                       // เปิดหน้าต่างใหม่เป็น ComboWindowSec
_fishInWindow = 0;                                                  // รีเซ็ตนับปลาในหน้าต่าง
_needFish = ComboFishRequired;                                      // HUD: ต้องกินอีกกี่ตัว
}
else
{
// ต่อเวลาให้สูงสุดไม่เกิน 20 วิ (กันค้างสั้นเกิน)
_windowLeft = Math.Max(_windowLeft, ComboWindowSec);                // ถ้ามีอยู่แล้ว ยื้อให้ยาวอย่างน้อยเท่า ComboWindowSec
}
_fishInWindow++;                                                    // เพิ่มจำนวนปลาที่กินในหน้าต่างนี้
EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, _needFish, _windowLeft); // แจ้ง HUD อัปเดตตัวคูณ

if (_fishInWindow >= ComboFishRequired)                             // ถึงโควตา → เพิ่มตัวคูณ
{
_mult = Math.Min(9, _mult + 1);         // เพิ่มคูณ (เพดาน 9)
_fishInWindow = 0;                      // รีเซ็ตนับรอบใหม่
_needFish = ComboFishRequired;          // HUD: รีเซ็ตความต้องการ
_windowLeft = ComboWindowSec;           // เริ่มรอบคอมโบใหม่เท่าเดิม
EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, _needFish, _windowLeft); // แจ้ง HUD อีกครั้ง
}

}
public void AddScore(int baseScore, object _unused) => AddScore(baseScore); // โอเวอร์โหลดเผื่อเรียกจาก signature อื่น

// ===== LIVES =====
public void LoseLife(int n = 1)                                          // ลดชีวิต n (อย่างน้อย 1)
{
if (InfiniteLives || IsGameOver || IsLevelCleared) return;           // ถ้าโกงชีวิต/จบ/ผ่าน ไม่หัก
_lives -= Math.Max(1, n);                                            // หักชีวิต
if (_lives < 0) _lives = 0;                                          // ไม่ให้ติดลบ
EmitSignal(SignalName.LivesChanged, _lives);                         // แจ้ง HUD
if (_lives <= 0) OnGameOver();                                       // ชีวิตหมด → จบเกม
}

// ===== CLEAR / GAMEOVER =====
private void OnLevelCleared()                                            // จัดการเมื่อผ่านด่าน
{
if (IsLevelCleared || IsGameOver) return;                                // กันเรียกซ้ำ
IsLevelCleared = true;                                                   // ตั้งสถานะผ่านด่าน
GameProgress.IsLevelCleared = true;  // ผ่านด่าน                      // ซิงค์ไปยังระบบความคืบหน้า

//  เพิ่มส่วนนี้ ↓↓↓
GameProgress.LastLevelScore = LevelScore;                                // บันทึกคะแนนด่านล่าสุด
GameProgress.LastBonusScore = _bonusScore;                                // บันทึกคะแนนโบนัสล่าสุด
GameProgress.LastTotalScore = GetTotalWithBonus();                        // บันทึกรวมคะแนนล่าสุด (รวมโบนัส)
GameProgress.LastHighScore = LoadHighScoreForLevel(Level);                // บันทึก high score ปัจจุบัน
GameProgress.Save();                                                      // เซฟความคืบหน้า

_isRunning = false;                                                       // หยุดตรรกะประจำเฟรม

GD.Print($"[ScoreManager] Level {Level} cleared!");                    // ดีบั๊ก
EmitSignal(SignalName.LevelCleared, LevelScore, Level);                   // ยิงสัญญาณให้ HUD/ระบบอื่น
}

private void OnGameOver()                                                // จัดการเมื่อเกมจบ
{
if (IsGameOver || IsLevelCleared) return;                            // กันเรียกซ้ำ
IsGameOver = true;                                                   // ตั้งสถานะจบเกม
GameProgress.IsLevelCleared = false;  // แพ้ = ไม่ผ่าน             // ซิงค์ความคืบหน้า
_isRunning = false;                                                  // หยุดตรรกะ

GD.Print($"[ScoreManager]Game Over at Level {Level}");           // ดีบั๊ก
EmitSignal(SignalName.GameOver, LevelScore, Level);                  // แจ้ง HUD/ระบบอื่น
}

public void SetRunning(bool run)                                         // เปิด/ปิดตรรกะเฟรม (แต่ห้ามเปิดถ้าจบ/ผ่าน)
{
_isRunning = run && !IsGameOver && !IsLevelCleared;
}

public void GoToNextLevel() => StartLevel(Level + 1);                    // ไปด่านถัดไป

// ===== SAVE / LOAD =====
private void LoadHighScore()                                             // โหลด HighScore จากไฟล์ง่าย ๆ
{
if (!FileAccess.FileExists(SAVE_FILE))                               // ถ้าไม่มีไฟล์
{
_highScore = 0;                                                  // ตั้งเป็นศูนย์
return;
}
using var f = FileAccess.Open(SAVE_FILE, FileAccess.ModeFlags.Read); // เปิดอ่าน
if (f == null) { _highScore = 0; return; }                           // เปิดไม่ได้ → ศูนย์
_highScore = (int)f.Get32();                                         // อ่านค่า 32 บิตเป็น int
}

private void SaveHighScore()                                             // บันทึก HighScore
{
using var f = FileAccess.Open(SAVE_FILE, FileAccess.ModeFlags.Write); // เปิดเขียน (ทับไฟล์)
f.Store32((uint)_highScore);                                         // เก็บค่า 32 บิต
}

// ===== GETTERS =====
public float GetTimeLeft() => TimeLeftSec;                               // คืนค่าเวลาเหลือ
public int GetBonusScore() => _bonusScore;                                // คืนคะแนนโบนัส
public int GetTotalWithBonus() => TotalScore + _bonusScore;              // รวมคะแนน + โบนัส
public int LoadHighScoreForLevel(int _level) => _highScore;              // (ปัจจุบันใช้ HighScore เดียวทุกเลเวล)

public void SyncRequestFromHud()                                         // HUD ขอซิงก์ค่าปัจจุบันทั้งหมด
{
EmitSignal(SignalName.TimeLeftChanged, TimeLeftSec);                 // เวลา
EmitSignal(SignalName.ScoreChanged, LevelScore, TargetScore);        // คะแนนด่าน/เป้า
EmitSignal(SignalName.TotalScoreChanged, TotalScore, _highScore);    // คะแนนรวม/ไฮสกอร์
EmitSignal(SignalName.LivesChanged, _lives);                         // ชีวิต
EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, _needFish, _windowLeft); // ตัวคูณ/คอมโบ
EmitSignal(SignalName.LevelChanged, Level);                          // เลเวล
}

// ===== MULTIPLIER =====
public void AddMultiplierFromCrystal(int add = 1)                        // เพิ่มตัวคูณจากผลของคริสตัล
{
int before = _mult;                                                  // เก็บค่าเดิม
_mult = Math.Clamp(_mult + Math.Max(1, add), 1, 9);                  // เพิ่มอย่างน้อย 1 และไม่เกิน 9
if (_mult != before)                                                 // ถ้ามีการเปลี่ยนแปลง
EmitSignal(SignalName.MultiplierChanged, _mult, _fishInWindow, _needFish, _windowLeft); // แจ้ง HUD
}
public void AddMultiplierFromCrystal(CrystalType crystal)                // เวอร์ชันระบุสี: Pink +2, อื่น ๆ +1
{
int delta = (crystal == CrystalType.Pink) ? 2 : 1;
AddMultiplierFromCrystal(delta);
}
public void AddMultiplierFromCrystal(object _) => AddMultiplierFromCrystal(1); // โอเวอร์โหลดเผื่อ signature อื่น

private CrystalType[] MapColors(CrystalType[] src)                       // ฟังก์ชันแม็ปสี (ปัจจุบันไม่ได้เรียกใช้แล้ว)
{
if (src == null || src.Length == 0) return Array.Empty<CrystalType>(); // ถ้าไม่มีข้อมูล → คืนลิสต์ว่าง
var dst = new CrystalType[src.Length];
for (int i = 0; i < src.Length; i++)
{
// แปลงโดยอาศัยชื่อ enum ให้เหมือนกัน (Purple, Blue, Green, Yellow, Red, Pink)
dst[i] = (CrystalType)Enum.Parse(typeof(CrystalType), src[i].ToString()); // แปลงข้อความเป็น enum เดิม (ที่จริงไม่จำเป็นถ้าชนิดเดียวกัน)
}
return dst;
}
// ===== BONUS SCORE =====
public void AddBonusScore(int value)                                       // เพิ่มคะแนนโบนัส (และแจ้ง HUD)
{
_bonusScore += value;
EmitSignal(SignalName.BonusScoreChanged, _bonusScore);
}

}
