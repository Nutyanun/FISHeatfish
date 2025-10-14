using Godot;
using System;
using System.Collections.Generic; // <- ใช้ Dictionary/List
using Game;                       // <- enum กลาง: CrystalType

public partial class SkillManager : Node
{
	public event Action<CrystalType, float> OnSkillStarted;
	public event Action<CrystalType> OnSkillEnded;

	// ใช้ ulong ให้ตรงกับ Time.GetTicksMsec()
	private readonly Dictionary<CrystalType, ulong> _activeUntil = new();
	private Player _player;

	public override void _Ready()
	{
		_player = GetParent<Player>();
		if (_player == null)
			GD.PushWarning("[SkillManager] Please add this node as a child of Player.");
	}

	public override void _Process(double delta)
	{
		if (_activeUntil.Count == 0) return;

		ulong now = Time.GetTicksMsec();
		var toStop = new List<CrystalType>();
		foreach (var kv in _activeUntil)
			if (now >= kv.Value) toStop.Add(kv.Key);

		foreach (var t in toStop) StopSkill(t);
	}

	// ให้คริสตัลเรียกตัวนี้
	public void Apply(CrystalType t, float durationSeconds)
	{
		// 🔴 Red: หักเวลา 10 วินาที "ทันที"
		if (t == CrystalType.Red)
		{
			ChangeTime(-10.0);
			OnSkillStarted?.Invoke(t, 0f);
			OnSkillEnded?.Invoke(t);
			return;
		}

		// 🟣 Purple: เพิ่มตัวคูณทันที +1 และรีเซ็ตหน้าต่างคอมโบ (20 วิ) — ไม่มีสถานะค้าง
		if (t == CrystalType.Purple)
		{
			IncreaseMultiplierImmediate();
			OnSkillStarted?.Invoke(t, 0f);
			OnSkillEnded?.Invoke(t);
			return;
		}

		// สีอื่น ๆ ทำงานตามปกติ (Blue/Green/Pink เป็นสถานะชั่วคราว)
		ulong now = Time.GetTicksMsec();
		ulong endAt = now + (ulong)Math.Round(durationSeconds * 1000.0);

		_activeUntil[t] = endAt;   // ใส่/รีเฟรช
		StartSkill(t);
		OnSkillStarted?.Invoke(t, durationSeconds);
	}

	public void StopSkill(CrystalType t)
	{
		if (!_activeUntil.Remove(t)) return;

		switch (t)
		{
			case CrystalType.Red:    /* ไม่มีสถานะค้าง */                   break;
			case CrystalType.Blue:   _player?.SetTimeFreeze(false);          break;
			case CrystalType.Green:  _player?.RemoveThornIfAny();            break;
			case CrystalType.Pink:   _player?.SetMagnet(false, 0f, 0f);      break;
			case CrystalType.Purple: /* ทันที ไม่มีสถานะค้าง */              break;
		}
		OnSkillEnded?.Invoke(t);
	}

	public void StopAll()
	{
		var keys = new List<CrystalType>(_activeUntil.Keys);
		foreach (var k in keys) StopSkill(k);
	}

	private void StartSkill(CrystalType t)
	{
		switch (t)
		{
			case CrystalType.Red:    /* ทันที ไม่มีสถานะค้าง */               break;
			case CrystalType.Blue:   _player?.SetTimeFreeze(true, 0.35f);     break;
			case CrystalType.Green:  _player?.GiveThornShield(1);             break;
			case CrystalType.Pink:   _player?.SetMagnet(true, 260f, 0.15f);   break;
			case CrystalType.Purple: /* ทันที ไม่มีสถานะค้าง */               break;
		}
	}

	// ===== ปรับเวลาในด่าน (บวก/ลบ วินาที) =====
	private void ChangeTime(double seconds)
	{
		if (Math.Abs(seconds) < 0.0001) return;

		// หา ScoreManager แบบยืดหยุ่น
		Node sm =
			GetTree().CurrentScene?.FindChild("ScoreManager", true, false) ??
			GetTree().Root?.FindChild("ScoreManager", true, false);

		if (sm == null)
		{
			GD.PushWarning("[SkillManager] ChangeTime: ScoreManager not found.");
			return;
		}

		// รองรับหลายชื่อเมธอดในโปรเจกต์
		if (sm.HasMethod("AddTime"))                 { sm.Call("AddTime", seconds); return; }
		if (sm.HasMethod("AddTimeSeconds"))          { sm.Call("AddTimeSeconds", seconds); return; }
		if (sm.HasMethod("AddBonusTime"))            { sm.Call("AddBonusTime", seconds); return; }
		if (sm.HasMethod("ReduceTime"))              { sm.Call("ReduceTime", Math.Abs(seconds)); return; }
		if (sm.HasMethod("SubtractTime"))            { sm.Call("SubtractTime", Math.Abs(seconds)); return; }

		GD.PushWarning("[SkillManager] No time API (AddTime/ReduceTime) on ScoreManager.");
	}

	// ===== เพิ่มคูณทันทีจากคริสตัลม่วง =====
	private void IncreaseMultiplierImmediate(int amount = 1)
	{
		// หา ScoreManager ให้ได้ตัวจริง (Node) แล้วเรียกเมธอดเฉพาะ
		ScoreManager sm =
			GetTree().CurrentScene?.FindChild("ScoreManager", true, false) as ScoreManager ??
			GetTree().Root?.FindChild("ScoreManager", true, false) as ScoreManager ??
			_player?.GetNodeOrNull<ScoreManager>("%ScoreManager");

		if (sm == null)
		{
			GD.PushWarning("[SkillManager] IncreaseMultiplierImmediate: ScoreManager not found.");
			return;
		}

		sm.AddMultiplierFromCrystal(amount);
	}
}
