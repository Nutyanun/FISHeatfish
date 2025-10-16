// res://scripts/FishSpawner.cs
using Godot;
using System;
using Game;

public partial class FishSpawner : Node2D
{
	[Export] public PackedScene[] FishScenes;  // ลาก Fish_*.tscn หลายอันลงมา
	[Export] public float[] Weights;           // น้ำหนักสำหรับแต่ละซีน (ยาวเท่า FishScenes) เว้นว่าง = เท่ากันหมด

	[Export] public float SpawnInterval = 1.4f;
	[Export] public int MaxFish = 100;                 // จำกัดจำนวนรวม
	[Export] public int[] MaxPerSpecies;              // จำกัดรายชนิด (ยาวเท่า FishScenes) เว้นว่าง = ไม่จำกัดรายชนิด

	[Export] public float OffscreenMargin = 140f;     // สปอว์นนอกจอเล็กน้อย
	[Export] public Vector2 YRange = new(80, 620);    // ช่วง Y ที่อนุญาต
	[Export] public Vector2 SpeedRange = new(90, 170);
	[Export] public Vector2 WaveAmpRange = new(8, 24);
	[Export] public Vector2 WaveFreqRange = new(0.6f, 1.4f);

	// ==== Balance controls ====
	[Export] public int MaxPredatorsOnScreen = 2;   // จำกัดจำนวนปลาดุบนจอพร้อมกัน
	[Export] public int MinEdibleOnScreen = 3;      // อย่างน้อยต้องมีปลาที่กินได้บนจอ
	[Export] public string[] PredatorKeywords = new string[] {"saw", "angler" };


	// ==== เพิ่ม: การปลดล็อกคริสตัล ====
	[Export] public NodePath ScoreManagerPath { get; set; } = null; // ถ้าเว้นว่างจะลองหา %ScoreManager
	[Export] public int CrystalSpeciesIndex { get; set; } = -1;     // index ของซีนที่เป็น "คริสตัล" (-1 = ไม่มี)
	[Export] public int CrystalUnlockLevel  { get; set; } = 3;      // เริ่มปล่อยตั้งแต่เลเวลนี้ขึ้นไป

	// Restrict certain predators to appear from a specific level
	[Export] public int MinLevelForSawShark { get; set; } = 3;
	[Export] public int MinLevelForSeaAngler { get; set; } = 3;

	private ScoreManager _sm; // อ้างอิง ScoreManager เพื่อเช็คเลเวล/ปลดล็อก

	private readonly RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		ProcessMode = Node.ProcessModeEnum.Inherit;

		_rng.Randomize();

		var timer = new Timer
		{
			WaitTime = Mathf.Max(0.05f, SpawnInterval),
			Autostart = true,
			OneShot = false,
			ProcessCallback = Timer.TimerProcessCallback.Idle
		};
		AddChild(timer);
		timer.Timeout += SpawnFish;

		// หา ScoreManager (ถ้าไม่ได้ตั้ง NodePath จะลองหา by name)
		_sm = (!string.IsNullOrEmpty(ScoreManagerPath))
			? GetNodeOrNull<ScoreManager>(ScoreManagerPath)
			: GetNodeOrNull<ScoreManager>("%ScoreManager");
	}

	private void SpawnFish()
	{
		if (FishScenes == null || FishScenes.Length == 0) return;

		// จำกัดจำนวนรวม
		if (GetTree().GetNodesInGroup("fish").Count >= MaxFish) return;

		// เลือกชนิดปลาตามน้ำหนัก (คำนึงถึงการปลดล็อกคริสตัล)
		int speciesIndex = PickSpeciesIndex();
		
		// === Lock SawShark / SeaAngler before their min level ===
		if (IsRestrictedSpecies(speciesIndex))
{
		for (int tries = 0; tries < 10 && IsRestrictedSpecies(speciesIndex); tries++)
		speciesIndex = PickSpeciesIndex();

		// ถ้ายังสุ่มได้ชนิดที่ถูกล็อกอยู่ก็ข้ามรอบนี้ไปเลย (กันหลุด)
		if (IsRestrictedSpecies(speciesIndex))
			return;
}

		// === Balance: cap predators & keep edible floor ===
		if (IsPredatorIndex(speciesIndex) && CountPredatorsAlive() >= MaxPredatorsOnScreen)
		{
			// reroll to non-predator (up to 10 tries)
			for (int t = 0; t < 10; t++)
			{
				int alt = PickSpeciesIndex();
				if (!IsPredatorIndex(alt)) { speciesIndex = alt; break; }
			}
		}
		if (CountEdibleAlive() < MinEdibleOnScreen && IsPredatorIndex(speciesIndex))
		{
			for (int t = 0; t < 10; t++)
			{
				int alt = PickSpeciesIndex();
				if (!IsPredatorIndex(alt)) { speciesIndex = alt; break; }
			}
		}

		// จำกัดจำนวนรายชนิด (ถ้าตั้งไว้)
		if (MaxPerSpecies != null && speciesIndex < MaxPerSpecies.Length)
		{
			int cap = MaxPerSpecies[speciesIndex];
			if (cap > 0)
			{
				// นับรายชนิดผ่าน group เฉพาะ
				string speciesGroup = $"fish_species_{speciesIndex}";
				if (GetTree().GetNodesInGroup(speciesGroup).Count >= cap)
					return;
			} 
		}

		// ขอบจอ
		var view = GetViewportRect();
		float leftX  = view.Position.X - OffscreenMargin;
		float rightX = view.End.X + OffscreenMargin;

		bool fromLeft = _rng.Randf() < 0.5f;
		float x = fromLeft ? leftX : rightX;
		float y = Mathf.Clamp(_rng.RandfRange(YRange.X, YRange.Y), view.Position.Y, view.End.Y);

		// สร้างปลา
		var fish = FishScenes[speciesIndex].Instantiate<Fish>();
		AddChild(fish);

		// ใส่ group รวม + group รายชนิด
		fish.AddToGroup("fish");
		fish.AddToGroup($"fish_species_{speciesIndex}");

		// ตั้งตำแหน่ง + ค่าสุ่ม
		fish.GlobalPosition = new Vector2(x, y);

		Vector2 baseDir = fromLeft ? Vector2.Right : Vector2.Left;
		Vector2 jitter  = new(_rng.RandfRange(-0.22f, 0.22f), _rng.RandfRange(-0.12f, 0.12f));
		fish.Direction  = (baseDir + jitter).Normalized();

		fish.Speed         = _rng.RandfRange(SpeedRange.X, SpeedRange.Y);
		fish.WaveAmplitude = _rng.RandfRange(WaveAmpRange.X, WaveAmpRange.Y);
		fish.WaveFrequency = _rng.RandfRange(WaveFreqRange.X, WaveFreqRange.Y);

		// ให้สุ่มสกินภายในซีนของตัวเอง (ถ้าซีนมีหลายอนิเมชัน)
		fish.ApplyRandomSkin();
	}

	private int PickSpeciesIndex()
	{
		// เช็คว่าคริสตัลยังล็อกอยู่ไหม
		bool crystalLocked = false;
		if (CrystalSpeciesIndex >= 0 && FishScenes != null && CrystalSpeciesIndex < FishScenes.Length)
		{
			// ถ้ามี ScoreManager และเลเวลยังไม่ถึง → ล็อก
			if (_sm != null)
				crystalLocked = _sm.Level < CrystalUnlockLevel;
			else
				crystalLocked = true; // หา SM ไม่เจอ → เซฟไว้ก่อนถือว่ายังล็อก
		}

		// กรณีไม่ได้ตั้ง Weights (หรือยาวไม่เท่า) → สุ่มเท่ากัน แต่ "ตัดคริสตัลออก" ถ้ายังล็อก
		if (Weights == null || Weights.Length != FishScenes.Length)
		{
			System.Collections.Generic.List<int> pool = new();
			for (int i = 0; i < FishScenes.Length; i++)
			{
				if (crystalLocked && i == CrystalSpeciesIndex) continue;
				pool.Add(i);
			}
			// ถ้าโดนตัดหมด → fallback สุ่มจากทั้งหมด
			if (pool.Count == 0)
				return _rng.RandiRange(0, FishScenes.Length - 1);

			int pick = _rng.RandiRange(0, pool.Count - 1);
			return pool[pick];
		}

		// มี Weights → ใช้ roulette wheel โดย "ชั่งน้ำหนักเป็น 0" ให้คริสตัลถ้ายังล็อก
		float sum = 0f;
		for (int i = 0; i < Weights.Length; i++)
		{
			float w = Mathf.Max(0f, Weights[i]);
			if (crystalLocked && i == CrystalSpeciesIndex) w = 0f;
			sum += w;
		}
		if (sum <= 0f)
		{
			// น้ำหนักหลังตัดเป็น 0 หมด → fallback สุ่มเท่ากัน (ยกเว้นคริสตัลถ้ายังล็อก)
			System.Collections.Generic.List<int> pool = new();
			for (int i = 0; i < FishScenes.Length; i++)
			{
				if (crystalLocked && i == CrystalSpeciesIndex) continue;
				pool.Add(i);
			}
			if (pool.Count == 0)
				return _rng.RandiRange(0, FishScenes.Length - 1);

			int pick = _rng.RandiRange(0, pool.Count - 1);
			return pool[pick];
		}

		float r = _rng.Randf() * sum;
		float acc = 0f;
		for (int i = 0; i < Weights.Length; i++)
		{
			float w = Mathf.Max(0f, Weights[i]);
			if (crystalLocked && i == CrystalSpeciesIndex) w = 0f;
			acc += w;
			if (r <= acc) return i;
		}
		return Weights.Length - 1;
	}

	private bool IsPredatorIndex(int speciesIndex)
	{
		if (FishScenes == null || speciesIndex < 0 || speciesIndex >= FishScenes.Length) return false;
		var path = FishScenes[speciesIndex]?.ResourcePath?.ToLower() ?? string.Empty;
		foreach (var key in PredatorKeywords)
			if (!string.IsNullOrEmpty(key) && path.Contains(key)) return true;
		return false;
	}

	private int CountPredatorsAlive()
	{
		int c = 0;
		foreach (var n in GetTree().GetNodesInGroup("fish"))
		{
			if (n is Fish f)
			{
				var t = (f.FishType ?? string.Empty).ToLower();
				foreach (var key in PredatorKeywords)
					if (!string.IsNullOrEmpty(key) && t.Contains(key)) { c++; break; }
			}
		}
		return c;
	}

	private int CountEdibleAlive()
	{
		int edible = 0;
		foreach (var n in GetTree().GetNodesInGroup("fish"))
		{
			if (n is Fish f)
			{
				var t = (f.FishType ?? string.Empty).ToLower();
				bool pred = false;
				foreach (var key in PredatorKeywords)
					if (!string.IsNullOrEmpty(key) && t.Contains(key)) { pred = true; break; }
				if (!pred) edible++;
			}
		}
		return edible;
	}
	private bool IsRestrictedSpecies(int speciesIndex)
{
	if (FishScenes == null || speciesIndex < 0 || speciesIndex >= FishScenes.Length) return false;

	// ใช้ชื่อไฟล์ซีนเป็นตัวบ่งชี้
	string path = FishScenes[speciesIndex]?.ResourcePath?.ToLowerInvariant() ?? string.Empty;
	int level = (_sm != null) ? _sm.Level : 1;

	// รองรับได้ทั้ง saw+shark แยกคำ หรือ sawshark ติดกัน
	if ((path.Contains("saw") && path.Contains("shark")) || path.Contains("sawshark"))
		return level < MinLevelForSawShark;

	// รองรับ sea+angler หรือ seaangler
	if ((path.Contains("sea") && path.Contains("angler")) || path.Contains("seaangler"))
		return level < MinLevelForSeaAngler;

	return false;
}

}
