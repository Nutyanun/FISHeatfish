using Godot;

public partial class Bg : ParallaxBackground
{
	[Export] public Vector2 Speed = new(-120f, 0f); // ลองแรงๆ ให้เห็นผล

	public override void _Ready()
	{
		ProcessMode = Node.ProcessModeEnum.Always; // ให้เลื่อนแม้มี pause
	}

	public override void _Process(double delta)
	{
		ScrollOffset += Speed * (float)delta;
	}
}
