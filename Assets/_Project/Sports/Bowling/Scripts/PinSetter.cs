using System.Collections.Generic;
using UnityEngine;

namespace VRGame3.Sports.Bowling
{
    /// <summary>
    /// 10本のピンを並べ直したり、倒れたピンを片付けたりする装置。
    /// 投げる前に立っていたピンのうち、何本倒れたかを数える。
    /// </summary>
    public class PinSetter : MonoBehaviour
    {
        [Tooltip("並べるピン10本")]
        [SerializeField] private List<BowlingPin> pins = new List<BowlingPin>();

        // 投げる前に立っていたピン
        private readonly List<BowlingPin> standingAtStart = new List<BowlingPin>();

        public IReadOnlyList<BowlingPin> Pins => pins;

        /// <summary>10本すべてを置いた位置に立て直す。</summary>
        public void ResetRack()
        {
            foreach (BowlingPin pin in pins)
            {
                pin.ResetToSpot();
            }
        }

        /// <summary>投げる前に呼び、いま立っているピンを覚える。</summary>
        public void MarkStandingPins()
        {
            standingAtStart.Clear();
            foreach (BowlingPin pin in pins)
            {
                if (!pin.IsKnockedDown)
                {
                    standingAtStart.Add(pin);
                }
            }
        }

        /// <summary>MarkStandingPins のあとに倒れた本数。</summary>
        public int CountKnockedSinceMark()
        {
            int count = 0;
            foreach (BowlingPin pin in standingAtStart)
            {
                if (pin.IsKnockedDown)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 立っているピンの揺れが収まったか。倒れたピンは数に関係しないので見ない。
        /// </summary>
        public bool AreStandingPinsSettled(float maxSpeed = 0.05f, float maxAngularSpeed = 0.5f)
        {
            foreach (BowlingPin pin in pins)
            {
                if (pin.IsKnockedDown)
                {
                    continue;
                }

                Rigidbody body = pin.Body;
                if (body.linearVelocity.magnitude > maxSpeed || body.angularVelocity.magnitude > maxAngularSpeed)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>MarkStandingPins のあとに倒れたピンを results に入れる(演出で使う)。</summary>
        public void GetKnockedSinceMark(List<BowlingPin> results)
        {
            results.Clear();
            foreach (BowlingPin pin in standingAtStart)
            {
                if (pin.IsKnockedDown)
                {
                    results.Add(pin);
                }
            }
        }

        /// <summary>倒れたピンを片付け、立っているピンの揺れを止める。</summary>
        public void SweepKnockedPins()
        {
            foreach (BowlingPin pin in pins)
            {
                if (pin.IsKnockedDown)
                {
                    pin.Remove();
                }
                else
                {
                    pin.Calm();
                }
            }
        }
    }
}
