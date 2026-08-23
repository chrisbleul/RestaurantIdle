using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Bewegt einen Gast durch feste Wegpunkte: Eingang -> Station -> Ausgang.
    /// Kein echtes NavMesh/Pfadfinden -- eine statische, kleine Location
    /// braucht dafuer keine Graphensuche, direkte Zielpunkte reichen.
    ///
    /// PLANv3.md K2-Umbau: die eigentliche Auftragskette (bedient? wie lange
    /// gewartet? bezahlt?) orchestriert GameManager -- dieses Skript kennt nur
    /// Bewegung und die beiden Zustaende "laeuft" und "wartet", nicht WARUM
    /// es wartet oder WIE LANGE. Frueher hatte GuestMover einen eigenen festen
    /// Warte-Timer und lief danach automatisch weiter; das war der Kern des
    /// K2-Befunds ("Gast beruehrt nichts") und ist deshalb ersatzlos raus --
    /// GameManager beendet die Wartephase jetzt explizit ueber Leave(),
    /// entweder weil ein Verkauf stattgefunden hat oder weil die Geduld
    /// (GameManager.GuestPatienceSeconds) abgelaufen ist.
    /// </summary>
    public class GuestMover : MonoBehaviour
    {
        private const float BaseMoveSpeed = 1.8f;
        private const float ArrivalThreshold = 0.05f;

        /// <summary>
        /// Leichte Streuung pro Gast (siehe GameManager.SpawnGuest) -- eine
        /// Reihe exakt gleich schnell laufender Gaeste liest sich als
        /// Foerderband, nicht als Publikum.
        /// </summary>
        public float SpeedMultiplier { get; set; } = 1f;

        public enum Phase
        {
            WalkingToStation,
            Waiting,
            WalkingToExit,
        }

        public Phase CurrentPhase { get; private set; }

        /// <summary>Aktuelles Laufziel -- der Aufrufer kann damit pruefen, ob ein erneutes Redirect ueberhaupt noetig ist.</summary>
        public Vector3 CurrentTarget => stationPosition;

        /// <summary>True, sobald die Zielposition (Station oder Abbiegepunkt bei "kein Platz frei") erreicht ist.</summary>
        public bool HasArrivedAtStation { get; private set; }

        private Vector3 stationPosition;
        private Vector3 exitPosition;
        private bool waitsForService;

        /// <param name="waitsForService">
        /// true: echter Stationsbesuch, Gast bleibt nach Ankunft in Phase
        /// "Waiting", bis GameManager Leave() ruft (bedient oder Geduld
        /// abgelaufen). false: kein Platz frei (PLANv3 K2) -- der Gast laeuft
        /// nur bis zu einem Abbiegepunkt und dreht sofort sichtbar ab, ohne
        /// dass GameManager ihn extra verwalten muss.
        /// </param>
        public void Init(Vector3 entrancePosition, Vector3 stationPos, Vector3 exitPos, bool waitsForService)
        {
            transform.position = entrancePosition;
            stationPosition = stationPos;
            exitPosition = exitPos;
            this.waitsForService = waitsForService;
            CurrentPhase = Phase.WalkingToStation;
            HasArrivedAtStation = false;
        }

        /// <summary>
        /// Neues Ziel waehrend des Spiels -- gebraucht fuer die Warteschlange
        /// (PLANv3 Phase E, "echtes Raumlayout/Warteschlange"): ein
        /// anstehender Gast rueckt auf, wenn vor ihm jemand geht, und laeuft
        /// von seinem Warteplatz aus zur Station, sobald eine frei wird.
        /// Kein Zurueckholen eines Gastes, der schon auf dem Weg nach
        /// draussen ist -- der ist fuer diesen Besuch verloren.
        /// </summary>
        public void Redirect(Vector3 newTarget, bool waitsForService)
        {
            if (CurrentPhase == Phase.WalkingToExit)
            {
                return;
            }

            stationPosition = newTarget;
            this.waitsForService = waitsForService;
            CurrentPhase = Phase.WalkingToStation;
            HasArrivedAtStation = false;
        }

        /// <summary>Beendet die Wartephase -- bedient (Served) oder unbedient (Geduld abgelaufen). No-op, wenn der Gast noch unterwegs oder schon auf dem Weg raus ist.</summary>
        public void Leave()
        {
            if (CurrentPhase == Phase.Waiting)
            {
                CurrentPhase = Phase.WalkingToExit;
            }
        }

        private void Update()
        {
            switch (CurrentPhase)
            {
                case Phase.WalkingToStation:
                    MoveToward(stationPosition);
                    if (Reached(stationPosition))
                    {
                        HasArrivedAtStation = true;
                        CurrentPhase = waitsForService ? Phase.Waiting : Phase.WalkingToExit;
                    }

                    break;

                case Phase.Waiting:
                    // Wird extern beendet, siehe Leave().
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
            transform.position = Vector3.MoveTowards(transform.position, target, BaseMoveSpeed * SpeedMultiplier * Time.deltaTime);
        }

        private bool Reached(Vector3 target) => Vector3.Distance(transform.position, target) < ArrivalThreshold;
    }
}
