using System;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Presentation.World;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectPenguin.Tests.EditMode
{
    /// <summary>
    /// <see cref="PenguinActivity"/> の値がすべて、ペンギンの Animator のステートとして実在するかを確かめる。
    /// ステート名が食い違うと、Play しても何も起きないだけで気づきにくいため。
    /// </summary>
    public sealed class PenguinAnimatorStatesTests
    {
        [Test]
        public void EveryActivityHasState()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PenguinAnimatorStates.ControllerPath);
            Assert.That(controller, Is.Not.Null, $"{PenguinAnimatorStates.ControllerPath} が読み込めません");

            var states = controller.layers[0].stateMachine.states.Select(s => s.state.nameHash).ToHashSet();
            var missing = Enum.GetValues(typeof(PenguinActivity)).Cast<PenguinActivity>()
                .Where(a => !states.Contains(PenguinAnimatorStates.HashOf(a)))
                .Select(PenguinAnimatorStates.NameOf)
                .ToList();
            Assert.That(missing, Is.Empty, "コントローラーにないステートがあります");
        }

        [Test]
        public void HashMatchesAnimatorHash()
        {
            foreach (PenguinActivity a in Enum.GetValues(typeof(PenguinActivity)))
            {
                Assert.That(PenguinAnimatorStates.HashOf(a), Is.EqualTo(Animator.StringToHash(a.ToString())), a.ToString());
            }
        }
    }
}
