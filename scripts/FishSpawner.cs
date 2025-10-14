// res://scripts/FishSpawner.cs
using Godot;
using System;

public partial class FishSpawner : Node2D
{
	[Export] public PackedScene[] FishScenes;  // ลาก Fish_*.tscn หลายอันลงมา
	[Export] public float[] Weights;           // น้ำหนักสำหรับแต่ละซีน (ยาวเท่า FishScenes) เว้นว่าง = เท่ากันหมด

	[Export] public float SpawnInterval = 1.4f;
	[Export] public int MaxFish = 24;                 // จำกัดจำนวนรวม
	[Export] public int[] MaxPerSpecies;              // จำกัดรายชนิด (ยาวเท่า FishScenes) เว้นว่าง = ไม่จำกัดรายชนิด

	[Export] public float OffscreenMargin = 140f;     // สปอว์นนอกจอเล็กน้อย
	[Export] public Vector2 YRange = new(80, 620);    // ช่วง Y ที่อนุญาต
	[Export] public Vector2 SpeedRange = new(90, 170);
	[Export] public Vector2 WaveAmpRange = new(8, 24);
	[Export] public Vector2 WaveFreqRange = new(0.6f, 1.4f);

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
	}

	private void SpawnFish()
	{
		if (FishScenes == null || FishScenes.Length == 0) return;

		// จำกัดจำนวนรวม
		if (GetTree().GetNodesInGroup("fish").Count >= MaxFish) return;

		// เลือกชนิดปลาตามน้ำหนัก
		int speciesIndex = PickSpeciesIndex();

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
		if (Weights == null || Weights.Length != FishScenes.Length)
			return _rng.RandiRange(0, FishScenes.Length - 1); // เท่ากันหมด

		// roulette wheel
		float sum = 0f;
		foreach (var w in Weights) sum += Mathf.Max(0f, w);
		if (sum <= 0f) return _rng.RandiRange(0, FishScenes.Length - 1);

		float r = _rng.Randf() * sum;
		float acc = 0f;
		for (int i = 0; i < Weights.Length; i++)
		{
			acc += Mathf.Max(0f, Weights[i]);
			if (r <= acc) return i;
		}
		return Weights.Length - 1;
	}
}
