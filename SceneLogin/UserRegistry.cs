using Godot;
using System;
using System.Collections.Generic;

public partial class UserRegistry : Node
{
	// เก็บชื่อที่ถูกใช้แล้ว (เดโม: เริ่มด้วย 2 ชื่อ)
	private HashSet<string> _usernames = new HashSet<string>()
	{
		"Alice123",
		"Bibi007"
	};

	public bool Exists(string username) => _usernames.Contains(username);

	public bool Add(string username)
	{
		if (_usernames.Contains(username)) return false;
		_usernames.Add(username);
		return true;
	}
}
