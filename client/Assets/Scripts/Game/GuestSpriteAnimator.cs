using UnityEngine;

namespace RestaurantIdle.Game
{
    /// <summary>
    /// Nutzer-Feedback ("nichts passt optisch zusammen"): steuerte bisher
    /// ein flaches 2D-Billboard-Sprite (Kenney Toon Characters), das sich
    /// permanent zur Kamera drehte -- ein Stilbruch mitten in einer echten
    /// 3D-Szene aus Kenney-Furniture-Kit-Moebeln, unabhaengig vom gewaehlten
    /// 2D-Set. Instanziert jetzt stattdessen ein echtes 3D-Mini-Character-
    /// Modell (siehe GameAssets.InstantiateRandomCharacter) und dreht es zur
    /// tatsaechlichen Laufrichtung statt zur Kamera -- Billboard ist fuer
    /// ein 3D-Modell ohnehin unnoetig.
    ///
    /// Klassenname bewusst beibehalten (nicht in z. B. GuestVisual
    /// umbenannt) -- GameManager referenziert ihn an mehreren Stellen, eine
    /// Umbenennung waere reine Kosmetik ohne Verhaltensaenderung.
    /// </summary>
    [RequireComponent(typeof(GuestMover))]
    public class GuestSpriteAnimator : MonoBehaviour
    {
        /// <summary>
        /// Ab welchem Anteil verbrauchter Geduld der wartende Gast anfaengt,
        /// unruhig zu werden -- darunter steht er ruhig.
        /// </summary>
        private const float FidgetThreshold = 0.45f;
        private const float FidgetSlowDegreesPerSecond = 90f;
        private const float FidgetFastDegreesPerSecond = 260f;
        private const float FidgetAmplitudeDegrees = 10f;

        /// <summary>Wie schnell sich die Figur zur Laufrichtung dreht -- endlich statt sofort, sonst "snapt" sie bei jeder Zielaenderung sichtbar.</summary>
        private const float TurnDegreesPerSecond = 480f;

        /// <summary>
        /// 0 = gerade angekommen, 1 = geht gleich unbedient. Wird vom
        /// GameManager aus derselben Geduld gespeist, die auch der
        /// Geduldsbalken ueber der Station anzeigt (StationBadge).
        ///
        /// Bisher sah ein Gast, dessen Geduld fast aufgebraucht war, exakt
        /// aus wie einer, der gerade erst angekommen ist -- die Information
        /// stand ausschliesslich im UI-Schild. In einem Spiel, dessen
        /// aktive Entscheidung "wo tippe ich als naechstes hin?" lautet,
        /// gehoert sie an die Figur selbst.
        /// </summary>
        public float Impatience { get; set; }

        private GuestMover mover;
        private Transform model;
        private Vector3 lastPosition;

        /// <summary>
        /// Ersetzt Awake-basierte Instanziierung: GameManager setzt
        /// Zielgroesse/Einfaerbung erst NACH AddComponent, ein Awake haette
        /// das Modell bereits mit Default-Werten (Hoehe 0) angelegt.
        /// </summary>
        public void Init(float targetHeight, Color? tint)
        {
            mover = GetComponent<GuestMover>();
            model = GameAssets.InstantiateRandomCharacter(transform, targetHeight, tint);
            lastPosition = transform.position;
        }

        private void Update()
        {
            if (model == null)
            {
                return;
            }

            TurnTowardMovement();
            ApplyFidget();
        }

        /// <summary>Dreht die Figur zur tatsaechlichen Bewegungsrichtung statt zum Laufziel -- bei Ankunft/Richtungswechsel bleibt sie sonst fuer einen Frame in die alte Richtung ausgerichtet, bevor CurrentTarget nachzieht.</summary>
        private void TurnTowardMovement()
        {
            var delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;

            if (delta.sqrMagnitude < 0.0000001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(delta);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, TurnDegreesPerSecond * Time.deltaTime);
        }

        /// <summary>
        /// Leichtes Hin-und-her-Wackeln des Modells bei knapper Geduld statt
        /// des frueheren Sprite-Frame-Wechsels -- das Mini-Characters-Paket
        /// bringt keine Animationsclips mit (siehe README).
        /// </summary>
        private void ApplyFidget()
        {
            if (mover.CurrentPhase != GuestMover.Phase.Waiting || Impatience < FidgetThreshold)
            {
                if (model.localRotation != Quaternion.identity)
                {
                    model.localRotation = Quaternion.identity;
                }

                return;
            }

            // Je knapper die Geduld, desto schneller das Zappeln -- ein
            // stetiger Uebergang statt eines harten Zustandswechsels, damit
            // die Dringlichkeit ablesbar ist und nicht nur ihr Vorhandensein.
            var urgency = Mathf.InverseLerp(FidgetThreshold, 1f, Impatience);
            var speed = Mathf.Lerp(FidgetSlowDegreesPerSecond, FidgetFastDegreesPerSecond, urgency);
            var wiggle = Mathf.Sin(Time.time * speed * Mathf.Deg2Rad) * FidgetAmplitudeDegrees;
            model.localRotation = Quaternion.Euler(0f, wiggle, 0f);
        }
    }
}
