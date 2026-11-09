using UnityEngine;

namespace PhysicalAssembly
{
    // Optional build-only policy; worlds without it retain their collision rules.
    public interface IAssemblyCollisionRules
    {
        bool IsBuildBlocker(Collider collider);
    }

    public interface IBuildModeTarget
    {
        bool IsBuilding { get; }
        bool SupportsModeGesture { get; }
        ModeGestureDirection GestureDirection { get; }
        bool CanSwitchMode { get; }
        int BuildCyclesUsed { get; }
        BuildModeGesture ModeGesture { get; }
        ChallengeGestureFeedback GestureFeedback { get; }
        bool TrySwitchMode();
    }

    public interface IAssemblyInteractionRules
    {
        // PrepareGrab is a read-only guard. AssemblyWorld owns topology/ownership changes.
        bool PrepareGrab(int hand, AssemblyPart part, Pose pose);
        bool CanAttachFromHand(int hand);
        bool CanTransferHold(int fromHand, int toHand, AssemblyPart part);
        AssemblyPart BranchRoot { get; }
        bool CanAttach(AssemblyPart source, AssemblyPart target, AssemblySocket socket);
        bool CanDetach(AssemblyPart part);
        bool CanSearchTarget(AssemblyPart part, AssemblySocket socket, AssemblyPart source);
    }
}
