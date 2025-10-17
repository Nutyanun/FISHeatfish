// res://scripts/FishSpawner.cs
using Godot;                // ใช้คลาสหลักของ Godot (Node2D, Timer, Vector2 ฯลฯ)
using System;               // ฟีเจอร์พื้นฐานของ .NET
using Game;                 // ใช้ namespace Game (อาจมี ScoreManager หรือ GameProgress อยู่)

public partial class FishSpawner : Node2D
{
	[Export] public PackedScene[] FishScenes;   // ซีนปลาทั้งหมดที่สามารถสปอว์นได้
	[Export] public float[] Weights;            // น้ำหนักความน่าจะเป็นของแต่ละซีน (ถ้าเว้นว่าง = เท่ากัน)

	[Export] public float SpawnInterval = 1.4f; // เวลาห่างระหว่างการสปอว์นแต่ละตัว (วินาที)
	[Export] public int MaxFish = 100;          // จำกัดจำนวนปลารวมสูงสุดในฉาก
	[Export] public int[] MaxPerSpecies;        // จำกัดจำนวนปลาแต่ละชนิด (ตาม index ของ FishScenes)

	[Export] public float OffscreenMargin = 140f;  // ระยะที่สปอว์นนอกจอ (เพื่อให้ว่ายเข้ามา)
	[Export] public Vector2 YRange = new(80, 620); // ช่วงตำแหน่งแกน Y ที่อนุญาตให้เกิดปลา
	[Export] public Vector2 SpeedRange = new(90, 170); // ช่วงความเร็วสุ่มของปลา
	[Export] public Vector2 WaveAmpRange = new(8, 24); // ช่วงการแกว่งขึ้นลง
	[Export] public Vector2 WaveFreqRange = new(0.6f, 1.4f); // ช่วงความถี่ของการแกว่ง

	// ==== ตัวปรับบาลานซ์ ====
	[Export] public int MaxPredatorsOnScreen = 2;   // จำกัดจำนวนปลาดุ (นักล่า) ที่อยู่บนจอพร้อมกัน
	[Export] public int MinEdibleOnScreen = 3;      // อย่างน้อยต้องมีปลาที่กินได้เท่านี้ตัว
	[Export] public string[] PredatorKeywords = new string[] {"saw", "angler" }; // คำที่ใช้บ่งชี้ว่าซีนไหนเป็นนักล่า

	// ==== เพิ่ม: ระบบปลดล็อกคริสตัล ====
	[Export] public NodePath ScoreManagerPath { get; set; } = null; // อ้างถึง ScoreManager เพื่อดูเลเวล
	[Export] public int CrystalSpeciesIndex { get; set; } = -1;     // index ของซีนที่เป็น “คริสตัล”
	[Export] public int CrystalUnlockLevel  { get; set; } = 3;      // ปลดล็อกเมื่อถึงเลเวลนี้

	// จำกัดปลาดุบางชนิดให้โผล่เฉพาะหลังด่านที่กำหนด
	[Export] public int MinLevelForSawShark { get; set; } = 3;
	[Export] public int MinLevelForSeaAngler { get; set; } = 3;

	private ScoreManager _sm;                  // ใช้อ้างอิงข้อมูลเลเวลจาก ScoreManager
	private readonly RandomNumberGenerator _rng = new(); // ตัวสุ่มในเกม

	public override void _Ready()
	{
		ProcessMode = Node.ProcessModeEnum.Inherit; // ให้สืบโหมดการประมวลผลจาก parent
		_rng.Randomize();                           // สุ่ม seed ใหม่ทุกครั้ง

		var timer = new Timer                       // สร้าง Timer สำหรับ spawn อัตโนมัติ
		{
			WaitTime = Mathf.Max(0.05f, SpawnInterval), // เวลาห่างแต่ละรอบ
			Autostart = true,                            // เริ่มเองทันที
			OneShot = false,                             // ทำซ้ำตลอด
			ProcessCallback = Timer.TimerProcessCallback.Idle // ทำงานตอน idle (ไม่ใช่ physics)
		};
		AddChild(timer);             // เพิ่ม Timer เข้าเป็นลูกของ Node
		timer.Timeout += SpawnFish;  // ผูก event Timeout → เรียก SpawnFish ทุกครั้ง

		// หา ScoreManager ถ้าไม่ได้ตั้ง path ไว้
		_sm = (!string.IsNullOrEmpty(ScoreManagerPath))
			? GetNodeOrNull<ScoreManager>(ScoreManagerPath)
			: GetNodeOrNull<ScoreManager>("%ScoreManager");
	}

	private void SpawnFish() // ฟังก์ชันสร้างปลาลงฉาก
{
	if (FishScenes == null || FishScenes.Length == 0) return; // ถ้าไม่มีซีนปลาเลย → ไม่ทำอะไรแล้วออกทันที

	if (GetTree().GetNodesInGroup("fish").Count >= MaxFish) return; // ถ้าปลาในฉากเกินจำนวนสูงสุด → ไม่สปอว์นเพิ่ม

	int speciesIndex = PickSpeciesIndex(); // สุ่มชนิดปลาจากน้ำหนักและเงื่อนไขปลดล็อกคริสตัล

	if (IsRestrictedSpecies(speciesIndex)) // ถ้าปลาชนิดนี้ยังไม่ถึงเลเวลปลดล็อก
	{
		for (int tries = 0; tries < 10 && IsRestrictedSpecies(speciesIndex); tries++) // พยายามสุ่มใหม่ได้สูงสุด 10 ครั้ง
			speciesIndex = PickSpeciesIndex();
		if (IsRestrictedSpecies(speciesIndex)) return; // ถ้ายังสุ่มได้ชนิดที่ล็อกอยู่ → ข้ามรอบนี้ไปเลย
	}

	// ตรวจบาลานซ์จำนวนปลานักล่าในฉาก
	if (IsPredatorIndex(speciesIndex) && CountPredatorsAlive() >= MaxPredatorsOnScreen)
	{
		for (int t = 0; t < 10; t++) // พยายามสุ่มปลาใหม่สูงสุด 10 ครั้ง
		{
			int alt = PickSpeciesIndex();
			if (!IsPredatorIndex(alt)) { speciesIndex = alt; break; } // ถ้าไม่ใช่นักล่า → ใช้ชนิดนี้แทน
		}
	}

	// ถ้าปลาที่กินได้มีน้อยกว่าที่กำหนด และสุ่มได้ปลานักล่าอีก → เปลี่ยนเป็นปลากินได้
	if (CountEdibleAlive() < MinEdibleOnScreen && IsPredatorIndex(speciesIndex))
	{
		for (int t = 0; t < 10; t++)
		{
			int alt = PickSpeciesIndex();
			if (!IsPredatorIndex(alt)) { speciesIndex = alt; break; } // เปลี่ยนเป็นชนิดที่กินได้
		}
	}

	// จำกัดจำนวนรายชนิดของปลา (เช่น ปลาชนิดเดียวกันไม่เกิน X ตัว)
	if (MaxPerSpecies != null && speciesIndex < MaxPerSpecies.Length)
	{
		int cap = MaxPerSpecies[speciesIndex]; // จำนวนสูงสุดที่กำหนด
		if (cap > 0)
		{
			string speciesGroup = $"fish_species_{speciesIndex}"; // ตั้งชื่อกลุ่มรายชนิด
			if (GetTree().GetNodesInGroup(speciesGroup).Count >= cap) return; // ถ้าเต็มแล้ว → ข้าม
		}
	}

	// หาค่าขอบเขตของหน้าจอเพื่อสุ่มตำแหน่ง spawn
	var view = GetViewportRect();
	float leftX  = view.Position.X - OffscreenMargin;  // spawn นอกจอด้านซ้ายเล็กน้อย
	float rightX = view.End.X + OffscreenMargin;       // spawn นอกจอด้านขวาเล็กน้อย

	bool fromLeft = _rng.Randf() < 0.5f;              // 50% โอกาสว่าจะมาจากฝั่งซ้ายหรือขวา
	float x = fromLeft ? leftX : rightX;              // ตำแหน่ง X เริ่มต้น
	float y = Mathf.Clamp(_rng.RandfRange(YRange.X, YRange.Y), view.Position.Y, view.End.Y); // สุ่มตำแหน่ง Y ในช่วงที่กำหนด

	var fish = FishScenes[speciesIndex].Instantiate<Fish>(); // สร้างอินสแตนซ์ของปลาจากซีนที่เลือกได้
	AddChild(fish); // เพิ่มปลาเข้าเป็นลูกของ Node นี้ (เข้าสู่ฉาก)

	fish.AddToGroup("fish"); // เพิ่มเข้ากลุ่มรวมปลา
	fish.AddToGroup($"fish_species_{speciesIndex}"); // เพิ่มเข้ากลุ่มรายชนิดของตัวเอง

	fish.GlobalPosition = new Vector2(x, y); // ตั้งตำแหน่งเริ่มต้นของปลาในโลก

	Vector2 baseDir = fromLeft ? Vector2.Right : Vector2.Left; // ทิศทางหลักของปลา (ขวาหรือซ้าย)
	Vector2 jitter  = new(_rng.RandfRange(-0.22f, 0.22f), _rng.RandfRange(-0.12f, 0.12f)); // สุ่มเบี่ยงเล็กน้อย
	fish.Direction  = (baseDir + jitter).Normalized(); // รวมทิศทางหลักกับ jitter แล้วทำให้ normalized

	fish.Speed         = _rng.RandfRange(SpeedRange.X, SpeedRange.Y); // สุ่มค่าความเร็ว
	fish.WaveAmplitude = _rng.RandfRange(WaveAmpRange.X, WaveAmpRange.Y); // สุ่มระดับการแกว่งขึ้นลง
	fish.WaveFrequency = _rng.RandfRange(WaveFreqRange.X, WaveFreqRange.Y); // สุ่มความถี่ของการแกว่ง

	fish.ApplyRandomSkin(); // สุ่มสกิน (อนิเมชัน) ของปลาตัวนั้น
}


	// เลือกชนิดปลาตามน้ำหนัก (roulette wheel)
	private int PickSpeciesIndex()
	{
		bool crystalLocked = false; // ตัวแปรเช็คว่าคริสตัลถูกล็อกอยู่ไหม (ตามเลเวลของผู้เล่น)

		// ตรวจสอบว่ามี index ของคริสตัลที่ตั้งไว้และอยู่ในช่วงของ FishScenes หรือไม่
		if (CrystalSpeciesIndex >= 0 && FishScenes != null && CrystalSpeciesIndex < FishScenes.Length)
		{
			if (_sm != null)
				crystalLocked = _sm.Level < CrystalUnlockLevel; // ถ้ามี ScoreManager และเลเวลยังไม่ถึง → ล็อกคริสตัล
			else
				crystalLocked = true; // ถ้าไม่มี ScoreManager → ถือว่าคริสตัลล็อกไว้ก่อน
		}

		// กรณีไม่ได้ตั้งค่า Weights → สุ่มเท่ากันทุกชนิด แต่ข้ามคริสตัลถ้ายังล็อกอยู่
		if (Weights == null || Weights.Length != FishScenes.Length)
		{
			var pool = new System.Collections.Generic.List<int>(); // รายชื่อชนิดปลาที่สุ่มได้
			for (int i = 0; i < FishScenes.Length; i++) // วนทุกชนิดปลา
			{
				if (crystalLocked && i == CrystalSpeciesIndex) continue; // ถ้าคริสตัลยังล็อกอยู่ → ข้าม
				pool.Add(i); // เพิ่มชนิดปลานี้ในรายการสุ่ม
			}
			if (pool.Count == 0) // ถ้าไม่มีชนิดไหนเลยหลังจากข้าม
				return _rng.RandiRange(0, FishScenes.Length - 1); // สุ่มจากทั้งหมดแทน
			int pick = _rng.RandiRange(0, pool.Count - 1); // สุ่ม index จากรายการที่เหลือ
			return pool[pick]; // คืนค่าชนิดปลาที่สุ่มได้
		}

		// ถ้ามีการตั้งค่า Weights → ใช้ระบบ “roulette wheel” สุ่มตามน้ำหนัก
		float sum = 0f; // รวมค่าน้ำหนักทั้งหมด
		for (int i = 0; i < Weights.Length; i++)
		{
			float w = Mathf.Max(0f, Weights[i]); // ป้องกันน้ำหนักติดลบ (ถ้ามี)
			if (crystalLocked && i == CrystalSpeciesIndex) w = 0f; // ถ้าคริสตัลยังล็อก → ไม่คิดน้ำหนัก
			sum += w; // รวมค่าน้ำหนักของแต่ละชนิด
		}

		// ถ้ารวมค่าน้ำหนักได้ 0 (ไม่มีปลาที่สุ่มได้)
		if (sum <= 0f)
		{
			var pool = new System.Collections.Generic.List<int>(); // รายชื่อปลาที่สุ่มได้
			for (int i = 0; i < FishScenes.Length; i++)
			{
				if (crystalLocked && i == CrystalSpeciesIndex) continue; // ข้ามคริสตัลถ้าล็อก
				pool.Add(i); // เพิ่มลงในรายการสุ่ม
			}
			if (pool.Count == 0) // ถ้าไม่มีชนิดปลาเหลือเลย
				return _rng.RandiRange(0, FishScenes.Length - 1); // สุ่มจากทั้งหมด
			int pick = _rng.RandiRange(0, pool.Count - 1); // สุ่ม index จากรายการ
			return pool[pick]; // คืนค่าชนิดที่เลือกได้
		}

		// เริ่มหมุนวงล้อ (roulette wheel)
		float r = _rng.Randf() * sum; // สุ่มตัวเลขระหว่าง 0 → ค่าน้ำหนักรวมทั้งหมด
		float acc = 0f; // ตัวสะสมค่าน้ำหนัก
		for (int i = 0; i < Weights.Length; i++) // วนผ่านทุกชนิดปลา
		{
			float w = Mathf.Max(0f, Weights[i]); // ค่าน้ำหนักปัจจุบัน
			if (crystalLocked && i == CrystalSpeciesIndex) w = 0f; // ถ้าคริสตัลล็อก → ตัดน้ำหนักออก
			acc += w; // บวกน้ำหนักสะสม
			if (r <= acc) return i; // ถ้าจุดสุ่มอยู่ในช่วงน้ำหนักของชนิดนี้ → คืนค่านี้เลย
		}

		return Weights.Length - 1; // ถ้าไม่เข้าเงื่อนไขใดเลย (เผื่อ safety) → คืนค่าชนิดสุดท้าย
	}


	private bool IsPredatorIndex(int speciesIndex) // ตรวจว่าปลาชนิดนี้เป็นนักล่าหรือไม่ จากชื่อไฟล์ซีน
	{
		if (FishScenes == null || speciesIndex < 0 || speciesIndex >= FishScenes.Length) return false; // ถ้า index ไม่ถูกต้อง → ไม่ตรวจ
		var path = FishScenes[speciesIndex]?.ResourcePath?.ToLower() ?? string.Empty; // แปลง path ของซีนให้เป็นตัวพิมพ์เล็ก
		foreach (var key in PredatorKeywords) // วนตรวจคำที่ระบุว่าเป็นปลานักล่า (เช่น "saw", "angler")
			if (!string.IsNullOrEmpty(key) && path.Contains(key)) return true; // ถ้าเจอคำที่ตรง → เป็นนักล่า
		return false; // ไม่เจอคำใดเลย → ไม่ใช่นักล่า
	}


	private int CountPredatorsAlive() // นับจำนวนปลานักล่าที่มีอยู่ในฉากตอนนี้
	{
		int c = 0; // ตัวนับจำนวนปลาดุ
		foreach (var n in GetTree().GetNodesInGroup("fish")) // วนดูทุก Node ในกลุ่ม "fish"
		{
			if (n is Fish f) // ตรวจว่า node นั้นเป็นวัตถุประเภท Fish
			{
				var t = (f.FishType ?? string.Empty).ToLower(); // อ่านชื่อชนิดปลาแล้วแปลงเป็นตัวพิมพ์เล็ก
				foreach (var key in PredatorKeywords) // วนตรวจคำสำคัญที่บ่งบอกว่าเป็นนักล่า (เช่น "saw", "angler")
					if (!string.IsNullOrEmpty(key) && t.Contains(key)) { c++; break; } // ถ้าชื่อมีคำเหล่านี้ → เป็นนักล่า → เพิ่มจำนวน
			}
		}
		return c; // ส่งคืนจำนวนปลาดุทั้งหมดในฉาก
	}


	private int CountEdibleAlive() // นับจำนวนปลาที่ผู้เล่นสามารถกินได้ (ไม่ใช่นักล่า)
	{
		int edible = 0; // ตัวนับจำนวนปลาที่กินได้
		foreach (var n in GetTree().GetNodesInGroup("fish")) // วนดูทุก Node ที่อยู่ในกลุ่ม "fish"
		{
			if (n is Fish f) // ตรวจว่า Node นั้นเป็นวัตถุประเภท Fish
			{
				var t = (f.FishType ?? string.Empty).ToLower(); // อ่านชนิดปลา (FishType) แล้วแปลงเป็นตัวพิมพ์เล็ก
				bool pred = false; // ตัวแปรบอกว่าปลาเป็นนักล่าหรือไม่
				foreach (var key in PredatorKeywords) // วนตรวจคำสำคัญของปลานักล่า (เช่น "saw", "angler")
					if (!string.IsNullOrEmpty(key) && t.Contains(key)) { pred = true; break; } // ถ้าชื่อชนิดปลามีคำพวกนี้ → เป็นนักล่า
				if (!pred) edible++; // ถ้าไม่ใช่นักล่า → เพิ่มจำนวนปลาที่กินได้
			}
		}
		return edible; // ส่งคืนจำนวนปลาที่กินได้ทั้งหมดในฉาก
	}


	private bool IsRestrictedSpecies(int speciesIndex) // ตรวจว่าปลาชนิดนี้ถูกล็อกตามเลเวลที่กำหนดไว้หรือไม่ (เช่น sawshark, seaangler)
	{
		if (FishScenes == null || speciesIndex < 0 || speciesIndex >= FishScenes.Length) return false; // ถ้า index ไม่ถูกต้องหรือไม่มีซีน → ไม่ล็อก
		string path = FishScenes[speciesIndex]?.ResourcePath?.ToLowerInvariant() ?? string.Empty; // ดึง path ของซีนปลา
		int level = (_sm != null) ? _sm.Level : 1; // อ่านเลเวลจาก ScoreManager ถ้าไม่มีใช้ 1

		if ((path.Contains("saw") && path.Contains("shark")) || path.Contains("sawshark")) return level < MinLevelForSawShark; // ถ้าเป็น sawshark และเลเวลยังไม่ถึง → ล็อก
		if ((path.Contains("sea") && path.Contains("angler")) || path.Contains("seaangler")) return level < MinLevelForSeaAngler; // ถ้าเป็น seaangler และเลเวลยังไม่ถึง → ล็อก

		return false; // ไม่เข้าเงื่อนไขใด ๆ → ไม่ล็อก
	}
}
