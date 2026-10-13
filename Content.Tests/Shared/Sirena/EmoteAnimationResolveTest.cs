using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Sirena.Animations;
using NUnit.Framework;

namespace Content.Tests.Shared.Sirena
{
    [TestFixture]
    public sealed class EmoteAnimationResolveTest
    {
        [Test]
        public void JumpKeepsTheHop()
        {
            var steps = new List<EmoteAnimationStep>
            {
                Shift(0f, Vector2.Zero),
                Shift(0.125f, new Vector2(0f, 1f)),
                Shift(0.25f, Vector2.Zero),
            };

            Assert.That(EmoteAnimation.TryResolve(steps, out var plan, out var error), Is.True, error);
            Assert.That(plan.Poses.Count, Is.EqualTo(3));
            Assert.That(plan.Poses[1].Shift, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(plan.Poses[2].At, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void FlipEndsOnAFullTurn()
        {
            var steps = new List<EmoteAnimationStep>
            {
                Tilt(0f, 0f),
                Tilt(0.25f, 180f),
                Tilt(0.5f, 360f),
            };

            Assert.That(EmoteAnimation.TryResolve(steps, out var plan, out var error), Is.True, error);
            Assert.That(plan.Poses.Count, Is.EqualTo(3));
            Assert.That(plan.Poses[2].Tilt, Is.EqualTo(360f).Within(0.001f));
        }

        [Test]
        public void TurnEndsFacingTheSameWay()
        {
            var steps = new List<EmoteAnimationStep>();
            for (var i = 0; i < 9; i++)
                steps.Add(Face(0.075f * i, 90f * i));

            Assert.That(EmoteAnimation.TryResolve(steps, out var plan, out var error), Is.True, error);
            Assert.That(plan.Poses.Count, Is.EqualTo(9));
            Assert.That(plan.Poses[8].Face, Is.EqualTo(720f).Within(0.001f));
        }

        [Test]
        public void OpenTiltComesBack()
        {
            var steps = new List<EmoteAnimationStep> { Tilt(0.2f, 90f) };

            Assert.That(EmoteAnimation.TryResolve(steps, out var plan, out var error), Is.True, error);
            Assert.That(plan.Poses[plan.Poses.Count - 1].Tilt, Is.EqualTo(0f).Within(0.001f));
            Assert.That(plan.Poses[plan.Poses.Count - 1].At, Is.GreaterThan(0.2f));
        }

        [Test]
        public void SidewaysFaceIsRejected()
        {
            var steps = new List<EmoteAnimationStep> { Face(0.2f, 45f) };

            Assert.That(EmoteAnimation.TryResolve(steps, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo("поворот не кратен 90"));
        }

        [Test]
        public void ShiftPastTwoIsRejected()
        {
            var steps = new List<EmoteAnimationStep> { Shift(0.2f, new Vector2(0f, 3f)) };

            Assert.That(EmoteAnimation.TryResolve(steps, out _, out var error), Is.False);
            Assert.That(error, Is.EqualTo("сдвиг дальше 2"));
        }

        private static EmoteAnimationStep Shift(float at, Vector2 shift)
        {
            return new EmoteAnimationStep
            {
                At = at,
                Shift = shift,
            };
        }

        private static EmoteAnimationStep Tilt(float at, float tilt)
        {
            return new EmoteAnimationStep
            {
                At = at,
                Tilt = tilt,
            };
        }

        private static EmoteAnimationStep Face(float at, float face)
        {
            return new EmoteAnimationStep
            {
                At = at,
                Face = face,
            };
        }
    }
}
