using Godot;
using System;

public partial class CrystalDirector : Node
{
	[Export] public NodePath ScoreManagerPath { get; set; } = null;
	[Export] public NodePath SpawnerPath { get; set; } = null;

	private Node _sm;                  // ScoreManager (ไม่ต้องรู้ชนิดจริง)
	private CrystalSpawner _spawner;

	private int _lastLevel = -1;
	private float _prevTime = 9999f;
	private bool _pinkTriggered = false;

	public override void _Ready()
	{
		_sm = (ScoreManagerPath != null && !ScoreManagerPath.IsEmpty)
			  ? GetNodeOrNull(ScoreManagerPath)
			  : GetTree().CurrentScene?.FindChild("ScoreManager", true, false);

		_spawner = (SpawnerPath != null && !SpawnerPath.IsEmpty)
				   ? GetNodeOrNull(SpawnerPath) as CrystalSpawner
				   : GetTree().CurrentScene?.FindChild("CrystalSpawner", true, false) as CrystalSpawner;

		if (_sm == null) GD.PushWarning("[CrystalDirector] ScoreManager not found.");
		if (_spawner == null) GD.PushWarning("[CrystalDirector] CrystalSpawner not found.");

		// บังคับ Apply rule ครั้งแรกทันทีถ้ารู้เลเวล
		int lv = GetInt("Level", 1);
		ApplyLevelRule(lv);
		_lastLevel = lv;
		_pinkTriggered = false;
		_prevTime = GetFloat("TimeLeftSec", 9999f);
	}

	public override void _Process(double delta)
	{
		if (_sm == null || _spawner == null) return;

		int level = GetInt("Level", 1);
		float timeLeft = GetFloat("TimeLeftSec", 9999f);

		// เปลี่ยนด่าน → อัปเดตกติกา
		if (level != _lastLevel)
		{
			ApplyLevelRule(level);
			_lastLevel = level;
			_pinkTriggered = false;      // เริ่มนับใหม่ทุกด่าน
		}

		// เข้าช่วง 20 วิท้ายครั้งแรก → บังคับชมพู 1 ชิ้น
		//if (level >= 4 && !_pinkTriggered && _prevTime > 20f && timeLeft <= 20f)
		//{
			//_spawner.ForcePinkOnceInLastWindow();
			//_pinkTriggered = true;
		//}

		_prevTime = timeLeft;
	}

	private void ApplyLevelRule(int level)
	{
		// ด่าน 1–3: ไม่ปล่อยคริสตัล
		if (level <= 3)
		{
			_spawner.ApplyRule(Array.Empty<CrystalType>(), 60f, 0);
			_spawner.ResetPinkForced();
			return;
		}

		// ด่าน 4+: เพิ่มสีทีละด่าน
		CrystalType[] colors;
		int maxOnScreen;

		if (level == 4)
		{
			colors = new[] { CrystalType.Green, CrystalType.Blue };
			maxOnScreen = 2;
		}
		else if (level == 5)
		{
			colors = new[] { CrystalType.Green, CrystalType.Blue, CrystalType.Purple };
			maxOnScreen = 3;
		}
		else if (level == 6)
		{
			colors = new[] { CrystalType.Green, CrystalType.Blue, CrystalType.Purple, CrystalType.Red };
			maxOnScreen = 4;
		}
		else // 7+
		{
			colors = new[] { CrystalType.Green, CrystalType.Blue, CrystalType.Purple, CrystalType.Red, CrystalType.Pink };
			maxOnScreen = 5;
		}

		_spawner.ApplyRule(colors, 60f, maxOnScreen);
		_spawner.ResetPinkForced(); // รีเซ็ตธงชมพูของรอบใหม่
	}

	// --- Safe getters from ScoreManager (field หรือ property ก็ได้) ---
	private int GetInt(string name, int def)
	{
		try
		{
			Variant v = _sm?.Get(name) ?? def;
			return v.VariantType switch
			{
				Variant.Type.Int => (int)(long)v,
				Variant.Type.Float => (int)Mathf.Round((float)(double)v),
				_ => def
			};
		}
		catch { return def; }
	}

	private float GetFloat(string name, float def)
	{
		try
		{
			Variant v = _sm?.Get(name) ?? def;
			return v.VariantType switch
			{
				Variant.Type.Float => (float)(double)v,
				Variant.Type.Int => (float)(long)v,
				_ => def
			};
		}
		catch { return def; }
	}
}
