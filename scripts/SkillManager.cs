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
	[Export] public int MaxGreenShields { get; set; } = 2;   // โล่สะสมได้กี่อัน

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
	private  void ApplyBlueSpeed(float addSeconds, float multiplier)
	{
		if (_player == null) return;

		// ถ้ายังไม่เปิด ให้เริ่มบัฟและเซ็ตสปีด
		if (!_blueBoostActive)
		{
			_blueBoostActive = true;
			_blueBaseSpeed = _player.MaxSpeed;
			_player.MaxSpeed = _blueBaseSpeed * Math.Max(0.1f, multiplier);
			_blueBoostRemaining = 0f;

			// เริ่มลูปนับถอยหลัง
			_ = BlueCountdownLoop();
		}

		_blueBoostRemaining += Math.Max(0.01f, addSeconds);
		UpdateBlueHud();
	}
	// ===== PINK =====
private  void ApplyPink(float addSeconds)
{
	if (_player == null) return;

	if (!_pinkActive)
	{
		_pinkActive = true;
		_pinkRemaining = 0f;
		_player.SetMagnet(true, PinkMagnetRadius, PinkSpeedBoost);
		_ = PinkCountdownLoop();
	}

	_pinkRemaining += Math.Max(0.01f, addSeconds);
	ShowHud(CrystalType.Pink, _pinkRemaining);
}

private async Task PinkCountdownLoop()
{
	while (_pinkRemaining > 0f)
	{
		await Delay(0.1f);
		_pinkRemaining = Math.Max(0f, _pinkRemaining - 0.1f);
		ShowHud(CrystalType.Pink, _pinkRemaining);
	}
	_player?.SetMagnet(false, 0f, 0f);
	_pinkActive = false;
	_pinkRemaining = 0f;
	ClearHud(CrystalType.Pink);
}

// ===== PURPLE =====
private  void ApplyPurple(float addSeconds)
{
	if (_player == null) return;

	if (!_purpleActive)
	{
		_purpleActive = true;
		_purpleRemaining = 0f;
		_player.SetTimeFreeze(true, PurpleWorldScale);
		_ = PurpleCountdownLoop();
	}

	_purpleRemaining += Math.Max(0.01f, addSeconds);
	ShowHud(CrystalType.Purple, _purpleRemaining);
}

private async Task PurpleCountdownLoop()
{
	while (_purpleRemaining > 0f)
	{
		await Delay(0.1f);
		_purpleRemaining = Math.Max(0f, _purpleRemaining - 0.1f);
		ShowHud(CrystalType.Purple, _purpleRemaining);
	}
	_player?.SetTimeFreeze(false);
	_purpleActive = false;
	_purpleRemaining = 0f;
	ClearHud(CrystalType.Purple);
}


	private async Task BlueCountdownLoop()
	{
		while (_blueBoostRemaining > 0f)
		{
			await Delay(0.1f);
			_blueBoostRemaining = Math.Max(0f, _blueBoostRemaining - 0.1f);
			UpdateBlueHud();
		}
		// หมดเวลา → คืนค่า
		if (_player != null) _player.MaxSpeed = _blueBaseSpeed;
		_blueBoostActive = false;
		_blueBoostRemaining = 0f;
		ClearHud(CrystalType.Blue);
		GD.Print("[SkillManager] BLUE speed off");
	}

	private void UpdateBlueHud()
	{
		ShowHud(CrystalType.Blue, _blueBoostRemaining, managedByBlue:true);
		GD.Print($"[SkillManager] BLUE time left={_blueBoostRemaining:0.0}s");
	}

	// ========= PINK =========
	private async void DoPink_On(float dur)
	{
		_player?.SetMagnet(true, PinkMagnetRadius, PinkSpeedBoost);
		await Delay(dur);
		_player?.SetMagnet(false, 0f, 0f);
	}

	// ========= PURPLE =========
	private async void DoPurple_On(float dur)
	{
		_player?.SetTimeFreeze(true, 0.65f);
		await Delay(dur);
		_player?.SetTimeFreeze(false);
	}

	// ========= RED =========
	private void DoRed_Add()
	{
		if (_scoreMgr == null) return;
		double val = Math.Abs(RedTimeDeltaSec);
		if (_scoreMgr.HasMethod("AddTime")) _scoreMgr.Call("AddTime", val);
	}

	private void DoRed_Sub()
	{
		if (_scoreMgr == null) return;
		double val = Math.Abs(RedTimeDeltaSec);
		if (_scoreMgr.HasMethod("AddTime")) _scoreMgr.Call("AddTime", -val);
	}

	// ========= HUD helpers =========
	private void ShowHud(CrystalType type, float dur, string note = null, bool managedByBlue = false)
{
	if (_hud == null || !IsInstanceValid(_hud)) FindHud();
	if (_hud == null) return;

	// Blue/Pink/Purple — HUD แสดงตามเวลาปัจจุบัน เราไม่ตั้ง timer เคลียร์ซ้ำ
	if (type == CrystalType.Blue || type == CrystalType.Pink || type == CrystalType.Purple)
	{
		_hud.ShowBuff(type, dur, labelOverride: (string.IsNullOrEmpty(note) ? null : note));
		return;
	}

	// Green — ใช้ -1 เพื่อให้ HUD แสดง xN แบบค้าง
	if (type == CrystalType.Green && dur < 0f)
	{
		_hud.ShowBuff(CrystalType.Green, -1f);
		return;
	}

	// Red (และกรณีอื่น ๆ ที่เป็นแบบมีเวลา)
	_hud.ShowBuff(type, dur, labelOverride: (string.IsNullOrEmpty(note) ? null : note));

	if (dur > 0f)
	{
		var t = GetTree().CreateTimer(Math.Max(0.1f, dur));
		t.Timeout += () =>
		{
			if (_hud != null && IsInstanceValid(_hud)) _hud.ClearBuff(type);
		};
	}
}


	private void ClearHud(CrystalType type)
	{
		if (_hud == null || !IsInstanceValid(_hud)) return;
		_hud.ClearBuff(type);
	}

	private async Task Delay(float sec)
	{
		var t = GetTree().CreateTimer(Math.Max(0.05f, sec));
		await ToSignal(t, Timer.SignalName.Timeout);
	}
	public void OnShieldConsumed()
	{
	ClearHud(CrystalType.Green); // ลด xN ลง 1; เหลือ 0 ก็ลบการ์ด
	}
}
