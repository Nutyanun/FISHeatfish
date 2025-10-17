using Godot;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

public partial class SkillManager : Node
{
	[Export] public NodePath PlayerPath { get; set; } = null;

	// PINK
	[Export] public float PinkMagnetRadius = 260f;
	[Export] public float PinkSpeedBoost   = 0.15f;

	// RED
	[Export] public float RedTimeDeltaSec  = 10f;

	private Player _player;
	private Node _scoreMgr;
	private CrystalHud _hud;

	// ===== BLUE (speed) state =====
	private bool _blueBoostActive = false;
	private float _blueBoostRemaining = 0f;
	private float _blueBaseSpeed = 0f;
	[Export] public float BlueSpeedMultiplier = 50f;
	[Export] public float BlueDefaultDuration = 20f;

	// ==== PINK (Magnet) stacking ====
	[Export] public float PinkDefaultDuration = 20f;
	private bool  _pinkActive = false;
	private float _pinkRemaining = 0f;

	// ==== PURPLE (TimeFreeze) stacking ====
	[Export] public float PurpleDefaultDuration = 15f;
	[Export] public float PurpleWorldScale      = 0.65f;
	private bool  _purpleActive = false;
	private float _purpleRemaining = 0f;

	// ---- Config stack ได้ ----
	[Export] public int MaxGreenShields { get; set; } = 5;   // โล่สะสมได้กี่อัน
	private int _greenStacks = 0;

	// ---- State ต่อเอฟเฟกต์แบบ stack ได้ ----
	private sealed class FxState
	{
	public int Stacks = 0;     // จำนวนชิ้น (เช่น โล่)
	public float TimeLeft = 0; // เวลาเหลือ (สำหรับเอฟเฟกต์ที่ใช้เวลา)
	}

	// เก็บตามไอดีคริสตัล เช่น "Blue","Green","Purple","RedAdd","RedSub","Pink"
	private readonly Dictionary<string, FxState> _fx = new();

	public override void _Ready()
	{
	if (PlayerPath != null && !PlayerPath.IsEmpty)
	_player = GetNodeOrNull<Player>(PlayerPath);
	if (_player == null)
	_player = GetNodeOrNull<Player>("%Player");


	_scoreMgr = GetNodeOrNull<Node>("%ScoreManager")
	?? GetTree().CurrentScene?.GetNodeOrNull<Node>("ScoreManager")
	?? GetTree().Root.GetNodeOrNull<Node>("ScoreManager");

	FindHud();
	}

	private void FindHud()
	{
	_hud = GetNodeOrNull<CrystalHud>("%CrystalHud")
	?? GetTree().CurrentScene?.FindChild("CrystalHud", true, false) as CrystalHud
	?? GetTree().Root.GetNodeOrNull<CrystalHud>("CrystalHud");
	}

	// ========= PUBLIC APPLY =========
	public bool Apply(string id, float durationSec = -1f)
	{
	if (string.IsNullOrEmpty(id)) return false;
	string s = id.Trim().ToLowerInvariant();

	if (s == "blue")
	{
	float add = (durationSec > 0f) ? durationSec : BlueDefaultDuration;
	ApplyBlueSpeed(add, BlueSpeedMultiplier);
	return true;
	}
	if (s == "pink")
	{
	float add = (durationSec > 0f) ? durationSec : PinkDefaultDuration;
	ApplyPink(add);
	ShowHud(CrystalType.Pink, _pinkRemaining); // โชว์เวลา “ที่เหลือ” ปัจจุบัน
	return true;
	}
	if (s == "redadd" || s == "red+" || s == "r+" || s == "addtime")
	{
	DoRed_Add();
	ShowHud(CrystalType.Red, 5f, note: "+10s");
	return true;
	}
	if (s == "redsub" || s == "red-" || s == "r-" || s == "subtime")
	{
	DoRed_Sub();
	// แสดง "-10s" ค้าง 5 วินาที
	ShowHud(CrystalType.Red, 5f, note: "-10s");
	return true;
	}

	if (s == "green")
	{
	_player?.GiveThornShield(1);
	ShowHud(CrystalType.Green, -1f); // xN stacking shown by HUD itself
	return true;
	}
	if (s == "purple")
	{
	float add = (durationSec > 0f) ? durationSec : PurpleDefaultDuration;
	ApplyPurple(add);
	ShowHud(CrystalType.Purple, _purpleRemaining);
	return true;
	}

	GD.PushWarning($"[SkillManager] Unknown id: {id}");
	return false;

	// ภายใน Apply(...)
	if (s == "green" || s == "shield" || s == "g")
	{
	_player?.GiveThornShield(1);     // เพิ่มโล่เข้าตัวผู้เล่น
	ShowHud(CrystalType.Green, -1f); // ✅ เรียกทุกครั้งที่เก็บ เพื่อให้ HUD บวก xN
	return true;
	}
	}
	// ========= BLUE (Speed boost with duration stacking) =========
	private void ApplyBlueSpeed(float addSeconds, float multiplier)        // ฟังก์ชันใช้สำหรับเปิดหรือขยายระยะเวลา “บูสต์ความเร็ว” (Blue)
	{
	if (_player == null) return;                                       // ถ้ายังไม่มีอ้างอิง Player → ไม่ทำอะไรเลย

	// ถ้ายังไม่เปิดบัฟ (Blue boost) ให้เริ่มใหม่ตั้งแต่ต้น
	if (!_blueBoostActive)
	{
	_blueBoostActive = true;                                       // ตั้งสถานะว่าบูสต์น้ำเงิน “กำลังทำงาน”
	_blueBaseSpeed = _player.MaxSpeed;                             // จำค่าความเร็วพื้นฐานของผู้เล่นไว้ก่อนบูสต์
	_player.MaxSpeed = _blueBaseSpeed * Math.Max(0.1f, multiplier); // ปรับความเร็วของผู้เล่นตามค่าคูณ multiplier (กันค่าต่ำสุดไม่ให้เป็นศูนย์)
	_blueBoostRemaining = 0f;                                      // เริ่มนับเวลาใหม่ (ตอนนี้ยังไม่มีเวลาคงเหลือ)

	// เริ่มลูปนับถอยหลังแบบอะซิงโครนัส (ทำงานพื้นหลังไม่ขัดการเล่นเกม)
	_ = BlueCountdownLoop();                                       // ใช้ "_" เพื่อบอกว่าไม่สนผลลัพธ์ Task
	}

	_blueBoostRemaining += Math.Max(0.01f, addSeconds);               // เพิ่มเวลาบูสต์ (ถ้า addSeconds < 0.01 ก็ใช้ 0.01 เป็นขั้นต่ำ)
	UpdateBlueHud();                                                  // อัปเดต HUD ให้แสดงเวลาที่เหลือของบูสต์ Blue
	}

	// ===== PINK =====
	private void ApplyPink(float addSeconds)                             // ฟังก์ชันเปิดหรือขยายระยะเวลาเอฟเฟกต์ “แม่เหล็กดูดของ” (Pink)
	{
	if (_player == null) return;                                      // ถ้าไม่มี Player → ไม่ทำอะไรเลย

	if (!_pinkActive)                                                 // ถ้ายังไม่เปิดแม่เหล็ก
	{
	_pinkActive = true;                                           // ตั้งสถานะให้รู้ว่าแม่เหล็กกำลังทำงาน
	_pinkRemaining = 0f;                                          // เริ่มเวลาใหม่ (0 วินาที ณ ตอนเริ่มเปิด)
	_player.SetMagnet(true, PinkMagnetRadius, PinkSpeedBoost);    // เรียกฟังก์ชันใน Player เพื่อเปิดแม่เหล็ก พร้อมตั้งรัศมีดูดและบูสต์ความเร็ว
	_ = PinkCountdownLoop();                                      // เริ่มลูปนับเวลาถอยหลังของ Pink แบบ async (ทำงานพื้นหลัง)
	}

	_pinkRemaining += Math.Max(0.01f, addSeconds);                    // เพิ่มเวลาที่เหลือ (หรือเริ่มนับใหม่)
	ShowHud(CrystalType.Pink, _pinkRemaining);                        // อัปเดต HUD ให้แสดงเวลาที่เหลือปัจจุบัน
	}

	private async Task PinkCountdownLoop()                               // ลูปนับเวลาถอยหลังของแม่เหล็ก (async เพื่อให้รันคู่กับเกมได้)
	{
	while (_pinkRemaining > 0f)                                      // ทำซ้ำจนกว่าเวลาจะหมด
	{
	await Delay(0.1f);                                            // รอ 0.1 วินาที (async → ไม่หยุดเกม)
	_pinkRemaining = Math.Max(0f, _pinkRemaining - 0.1f);         // ลดเวลาที่เหลือลง 0.1 วินาทีต่อรอบ (ไม่ให้ติดลบ)
	ShowHud(CrystalType.Pink, _pinkRemaining);                    // อัปเดต HUD ให้แสดงเวลาที่เหลือล่าสุด
	}

	_player?.SetMagnet(false, 0f, 0f);                               // เมื่อหมดเวลา ปิดเอฟเฟกต์แม่เหล็ก (ส่ง false)
	_pinkActive = false;                                              // ตั้งสถานะว่า “หมดฤทธิ์” แล้ว
	_pinkRemaining = 0f;                                              // รีเซ็ตเวลาเหลือเป็นศูนย์
	ClearHud(CrystalType.Pink);                                       // สั่ง HUD ให้ลบไอคอน/ตัวจับเวลาของ Pink ออกจากหน้าจอ
	}

	// ===== PURPLE =====
	private void ApplyPurple(float addSeconds)                               // ฟังก์ชันใช้เปิดหรือขยายระยะเวลา “เอฟเฟกต์ชะลอเวลา” (Purple)
	{
	if (_player == null) return;                                         // ถ้าไม่มีอ้างอิง Player → ไม่ทำอะไรเลย

	if (!_purpleActive)                                                 // ถ้ายังไม่เปิดเอฟเฟกต์สีม่วง
	{
	_purpleActive = true;                                           // ตั้งสถานะว่ากำลังเปิดใช้งานเอฟเฟกต์
	_purpleRemaining = 0f;                                          // รีเซ็ตเวลาเริ่มต้นให้ 0
	_player.SetTimeFreeze(true, PurpleWorldScale);                  // สั่ง Player ให้เปิด “โหมดชะลอเวลา” ด้วยค่า scale ที่กำหนด (เช่น 0.65 = ช้าลง 35%)
	_ = PurpleCountdownLoop();                                      // เริ่มลูปนับถอยหลังแบบ async (ทำงานเบื้องหลังไม่ขัดเกม)
	}

	_purpleRemaining += Math.Max(0.01f, addSeconds);                    // เพิ่มเวลาที่เหลือของเอฟเฟกต์ (ขั้นต่ำ 0.01 วินาที)
	ShowHud(CrystalType.Purple, _purpleRemaining);                      // อัปเดต HUD ให้แสดงเวลาที่เหลือของ Purple
	}

	private async Task PurpleCountdownLoop()                                 // ลูปอะซิงโครนัสสำหรับนับเวลาถอยหลังของเอฟเฟกต์สีม่วง
	{
	while (_purpleRemaining > 0f)                                       // ทำซ้ำจนกว่าเวลาจะหมด
	{
	await Delay(0.1f);                                              // หน่วงเวลา 0.1 วินาที (แบบ async → ไม่หยุดเกม)
	_purpleRemaining = Math.Max(0f, _purpleRemaining - 0.1f);       // ลดเวลาที่เหลือทีละ 0.1 วินาที (กันค่าติดลบ)
	ShowHud(CrystalType.Purple, _purpleRemaining);                  // อัปเดต HUD ให้แสดงเวลาปัจจุบันของ Purple
	}

	_player?.SetTimeFreeze(false);                                      // เมื่อหมดเวลา สั่ง Player ให้ “ปิดโหมดชะลอเวลา”
	_purpleActive = false;                                              // ตั้งสถานะว่าไม่ทำงานแล้ว
	_purpleRemaining = 0f;                                              // รีเซ็ตเวลาเหลือเป็นศูนย์
	ClearHud(CrystalType.Purple);                                       // ลบไอคอน/แถบเวลา Purple ออกจาก HUD
	}

	// ===== BLUE Countdown =====
	private async Task BlueCountdownLoop()                                  // ลูปนับเวลาถอยหลังของ “บูสต์ความเร็ว” (Blue)
	{
	while (_blueBoostRemaining > 0f)                                    // ทำซ้ำจนกว่าเวลาบูสต์จะหมด
	{
	await Delay(0.1f);                                              // หน่วงเวลา 0.1 วินาที (ไม่ขัดเกม)
	_blueBoostRemaining = Math.Max(0f, _blueBoostRemaining - 0.1f); // ลดเวลาที่เหลือลง 0.1 วินาทีต่อรอบ (กันติดลบ)
	UpdateBlueHud();                                                // อัปเดต HUD ให้แสดงเวลาปัจจุบันของบูสต์ Blue
	}

	// ==== เมื่อหมดเวลา ====
	if (_player != null) _player.MaxSpeed = _blueBaseSpeed;             // คืนค่าความเร็วของ Player กลับเป็นค่าปกติ (ก่อนบูสต์)
	_blueBoostActive = false;                                          // ตั้งสถานะว่าบูสต์ไม่ทำงานแล้ว
	_blueBoostRemaining = 0f;                                          // รีเซ็ตตัวนับเวลา
	ClearHud(CrystalType.Blue);                                        // ลบไอคอนหรือแถบเวลาของ Blue ออกจาก HUD
	GD.Print("[SkillManager] BLUE speed off");                         // พิมพ์ข้อความ Debug ใน Output เพื่อบอกว่าบูสต์น้ำเงินสิ้นสุดแล้ว
	}

	private void UpdateBlueHud()                                                              // อัปเดต HUD ของบัฟสีน้ำเงิน (เรียกทุกครั้งที่เวลาคงเหลือเปลี่ยน)
	{
	ShowHud(CrystalType.Blue, _blueBoostRemaining, managedByBlue: true);                  // บอก HUD ให้โชว์สถานะ Blue พร้อมเวลาที่เหลือ (ส่งธง managedByBlue ไว้แยก logic ฝั่ง HUD ถ้าต้องการ)
	GD.Print($"[SkillManager] BLUE time left={_blueBoostRemaining:0.0}s");                // พิมพ์ดีบัก: เหลือเวลากี่วินาที (ทศนิยม 1 ตำแหน่ง)
	}

	// ========= PINK =========
	private async void DoPink_On(float dur)                                                   // Helper แบบ fire-and-forget: เปิดแม่เหล็กชั่วคราวตามระยะเวลา dur
	{
	_player?.SetMagnet(true, PinkMagnetRadius, PinkSpeedBoost);                           // เปิดโหมดแม่เหล็กให้ Player (กำหนดรัศมีและบูสต์ความเร็ว)
	await Delay(dur);                                                                     // รอเวลา dur วินาที (async ไม่บล็อกเกม)
	_player?.SetMagnet(false, 0f, 0f);                                                    // หมดเวลา → ปิดแม่เหล็ก และรีเซ็ตพารามิเตอร์
	}

	// ========= PURPLE =========
	private async void DoPurple_On(float dur)                                                 // Helper แบบ fire-and-forget: เปิดชะลอเวลา (Purple) ชั่วคราวตาม dur
	{
	_player?.SetTimeFreeze(true, 0.65f);                                                  // เปิดโหมดชะลอเวลา ใช้สเกล 0.65 (เกมช้าลง 35%)
	await Delay(dur);                                                                     // รอเวลา dur วินาที (async)
	_player?.SetTimeFreeze(false);                                                        // หมดเวลา → ปิดชะลอเวลา
	}

	// ========= RED =========
	private void DoRed_Add()                                                                  // เพิ่มเวลาเกม (เช่น +10s) โดยคุยกับ ScoreManager
	{
	if (_scoreMgr == null) return;                                                        // ถ้าไม่มี ScoreManager → ออก
	double val = Math.Abs(RedTimeDeltaSec);                                               // ใช้ค่าสัมบูรณ์เพื่อกันค่าติดลบ (ให้แน่ใจว่าเป็นบวก)
	if (_scoreMgr.HasMethod("AddTime")) _scoreMgr.Call("AddTime", val);                   // ถ้ามีเมธอด AddTime → เรียกด้วยค่า +val
	}

	private void DoRed_Sub()                                                                  // ลดเวลาเกม (เช่น −10s) โดยคุยกับ ScoreManager
	{
	if (_scoreMgr == null) return;                                                        // ถ้าไม่มี ScoreManager → ออก
	double val = Math.Abs(RedTimeDeltaSec);                                               // ใช้ค่าสัมบูรณ์จากคอนฟิก
	if (_scoreMgr.HasMethod("AddTime")) _scoreMgr.Call("AddTime", -val);                  // ถ้ามีเมธอด AddTime → เรียกด้วยค่า −val
	}

	// ========= HUD helpers =========
	private void ShowHud(CrystalType type, float dur, string note = null, bool managedByBlue = false)
	// โชว์/อัปเดตการ์ดสกิลบน HUD:
	//  - type = ชนิดคริสตัล
	//  - dur  = เวลา (วินาที) ถ้าเป็น −1f ใช้เป็นสัญญะ “ค้าง/นับ stack” (เช่น Green)
	//  - note = ข้อความเสริม (เช่น “+10s”)
	//  - managedByBlue = ธงเผื่อให้ HUD แยก logic ของ Blue ที่อัปเดตถี่ ๆ จากลูปของมันเอง
	{
	if (_hud == null || !IsInstanceValid(_hud)) FindHud();                                 // ถ้า HUD ยังไม่ถูกอ้างอิงหรือถูกลบไปแล้ว → ลองหาใหม่
	if (_hud == null) return;                                                              // หาไม่เจอจริง ๆ ก็ออก

	// Blue/Pink/Purple — เวลาเปลี่ยนตลอดจากลูปนับถอยหลัง เราจึงสั่ง HUD “อัปเดตรายครั้ง” ไม่ตั้งตัวจับเวลาเคลียร์ฝั่งนี้
	if (type == CrystalType.Blue || type == CrystalType.Pink || type == CrystalType.Purple)
	{
	_hud.ShowBuff(type, dur, labelOverride: (string.IsNullOrEmpty(note) ? null : note)); // ให้ HUD แสดงการ์ดพร้อมเวลาปัจจุบัน (และ label ถ้ามี)
	return;                                                                             // จบเคสนี้
	}

	// Green — ใช้ dur < 0 เพื่อบอก HUD ให้แสดงเป็น xN (stack) แบบค้าง ไม่ใช่ตัวจับเวลา
	if (type == CrystalType.Green && dur < 0f)
	{
	_hud.ShowBuff(CrystalType.Green, -1f);                                              // ส่ง −1f เป็นสัญญะ “ค้าง/นับจำนวน”
	return;                                                                             // จบเคส Green
	}

	// Red (และกรณีอื่น ๆ ที่ตั้งเวลาจบชัดเจน) — ให้ HUD แสดงตาม dur และตั้งตัวจับเวลาไว้เคลียร์การ์ดเมื่อหมดเวลา
	_hud.ShowBuff(type, dur, labelOverride: (string.IsNullOrEmpty(note) ? null : note));    // สั่ง HUD แสดงการ์ดพร้อมเวลาที่ตั้ง

	if (dur > 0f)                                                                           // ถ้าระบุเวลามาเป็นบวก
	{
	var t = GetTree().CreateTimer(Math.Max(0.1f, dur));                                 // สร้าง Timer แบบ one-shot รออย่างน้อย 0.1s เพื่อกันค่าแปลก ๆ
	t.Timeout += () =>                                                                   // เมื่อตัวจับเวลาหมด
	{
	if (_hud != null && IsInstanceValid(_hud)) _hud.ClearBuff(type);                // เคลียร์การ์ดของ type บน HUD (ถ้า HUD ยังอยู่)
	};
	}
	}

	private void ClearHud(CrystalType type)                                                     // ล้างการ์ดสกิลของชนิดที่ระบุออกจาก HUD
	{
	if (_hud == null || !IsInstanceValid(_hud)) return;                                     // ถ้า HUD ไม่มีหรือโดนลบ → ออก
	_hud.ClearBuff(type);                                                                   // สั่ง HUD ล้างการ์ดชนิดนั้น
	}

	private async Task Delay(float sec)                                                         // Helper หน่วงเวลาแบบ async (ใช้กับลูปนับถอยหลัง/เอฟเฟกต์ชั่วคราว)
	{
	var t = GetTree().CreateTimer(Math.Max(0.05f, sec));                                    // สร้าง Timer one-shot หน่วงอย่างน้อย 0.05s
	await ToSignal(t, Timer.SignalName.Timeout);                                            // รอจนกว่าจะได้สัญญาณ Timeout (async)
	}

	public void OnShieldConsumed()                                                              // Hook เมื่อโล่ถูกใช้/แตกไป 1 ชิ้น
	{
	ClearHud(CrystalType.Green);                                                            // ให้ HUD ลด xN ลง (และถ้าเหลือ 0 → HUD อาจลบการ์ด)
	}
}
