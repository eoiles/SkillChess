// ChessFeedbackHub.cs
using UnityEngine;
using MoreMountains.Feedbacks;

public class ChessFeedbackHub : MonoBehaviour
{
    [Header("Gameplay")]
    public MMF_Player Select;
    public MMF_Player Move;
    public MMF_Player Capture;
    public MMF_Player Illegal;
    public MMF_Player Check;
    public MMF_Player Checkmate;
    public MMF_Player Stalemate;
    public MMF_Player Promote;
    public MMF_Player Restart;

    // --- basic plays ---
    public void PlaySelect()    => Select?.PlayFeedbacks();
    public void PlayMove()      => Move?.PlayFeedbacks();
    public void PlayCapture()   => Capture?.PlayFeedbacks();
    public void PlayIllegal()   => Illegal?.PlayFeedbacks();
    public void PlayCheck()     => Check?.PlayFeedbacks();
    public void PlayCheckmate() => Checkmate?.PlayFeedbacks();
    public void PlayStalemate() => Stalemate?.PlayFeedbacks();
    public void PlayPromote()   => Promote?.PlayFeedbacks();
    public void PlayRestart()   => Restart?.PlayFeedbacks();

    // --- play at world position (for particle spawn / 3D effects) ---
    void PlayAt(MMF_Player player, Vector3 position)
    {
        if (player == null) return;

        Transform t = player.transform;
        Vector3 oldPos = t.position;
        t.position = position;

        player.PlayFeedbacks();

        // Return so we don't leave FX objects scattered around the board
        t.position = oldPos;
    }

    public void PlaySelectAt(Vector3 pos)  => PlayAt(Select, pos);
    public void PlayMoveAt(Vector3 pos)    => PlayAt(Move, pos);
    public void PlayCaptureAt(Vector3 pos) => PlayAt(Capture, pos);
}
