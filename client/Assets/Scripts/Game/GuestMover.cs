using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Bewegt einen Gast durch feste Wegpunkte: Eingang -> Station (mit
    /// Wartepause) -> Ausgang (PLANv2.md Abschnitt 9: "Wegfindung Eingang ->
    /// Warteschlange -> Theke -> Ausgang"). Kein echtes NavMesh/Pfadfinden --
    /// eine statische, kleine Location braucht dafuer keine Graphensuche,
    /// direkte Zielpunkte reichen.
    /// </summary>
    public class GuestMover : MonoBehaviour
    {
        private const float MoveSpeed = 1.8f;
        private const float WaitSecondsAtStation = 1.2f;
        private const float ArrivalThreshold = 0.05f;

        private enum Phase
        {
            WalkingToStation,
            Waiting,
            WalkingToExit,
        }

        private Vector3 stationPosition;
        private Vector3 exitPosition;
        private Phase phase;
        private float waitTimer;

        public void Init(Vector3 entrancePosition, Vector3 stationPos, Vector3 exitPos)
        {
            transform.position = entrancePosition;
            stationPosition = stationPos;
            exitPosition = exitPos;
            phase = Phase.WalkingToStation;
        }

        private void Update()
        {
            switch (phase)
            {
                case Phase.WalkingToStation:
                    MoveToward(stationPosition);
                    if (Reached(stationPosition))
                    {
                        phase = Phase.Waiting;
                        waitTimer = WaitSecondsAtStation;
                    }

                    break;

                case Phase.Waiting:
                    waitTimer -= Time.deltaTime;
                    if (waitTimer <= 0f)
                    {
                        phase = Phase.WalkingToExit;
                    }

                    break;

                case Phase.WalkingToExit:
                    MoveToward(exitPosition);
                    if (Reached(exitPosition))
                    {
                        Destroy(gameObject);
                    }

                    break;
            }
        }

        private void MoveToward(Vector3 target)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, MoveSpeed * Time.deltaTime);
        }

        private bool Reached(Vector3 target) => Vector3.Distance(transform.position, target) < ArrivalThreshold;
    }
}
