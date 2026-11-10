namespace Celeste64;

/// <summary>Requires triggers held during a wall jump or dash to be released before they grab again.</summary>
public sealed class VrGrabReleaseGate
{
	public bool LeftBlocked { get; private set; }
	public bool RightBlocked { get; private set; }
	public void BlockHeld(bool leftHeld, bool rightHeld) { LeftBlocked = leftHeld; RightBlocked = rightHeld; }
	public void Update(bool leftTracked, bool leftHeld, bool rightTracked, bool rightHeld)
	{
		if (leftTracked && !leftHeld) LeftBlocked = false;
		if (rightTracked && !rightHeld) RightBlocked = false;
	}
	public void Reset() { LeftBlocked = RightBlocked = false; }
}
