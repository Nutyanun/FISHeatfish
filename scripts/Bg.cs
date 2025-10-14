using Godot;

public partial class Bg : ParallaxBackground 
{
	[Export] public Vector2 Speed = new(-120f, 0f); // ความเร็วเลื่อนฉาก

	public override void _Ready()
	{
		// ให้พื้นหลังเลื่อนต่อแม้เกมถูก pause
		ProcessMode = Node.ProcessModeEnum.Always;
	}

	public override void _Process(double delta)
	{
		ScrollOffset += Speed * (float)delta;
	}
}
