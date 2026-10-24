using NUnit.Framework;
using ProjectPenguin.Presentation.World;
using UnityEngine;

namespace ProjectPenguin.Tests.EditMode
{
    /// <summary>
    /// 施設の確認シーンのギャラリー。EditMode では Start が呼ばれないので、UI を組まずに
    /// <see cref="FacilityDesignGallery.Configure"/> と <see cref="FacilityDesignGallery.Select"/> を直接呼んで確かめる。
    /// </summary>
    public sealed class FacilityDesignGalleryTests
    {
        private GameObject _root;
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ReviewGallery");
            _camera = new GameObject("ReviewCamera").AddComponent<Camera>();
            _camera.transform.SetParent(_root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        private FacilityDesignGallery Gallery(int count)
        {
            var facilities = new Transform[count];
            var labels = new string[count];
            for (var i = 0; i < count; i++)
            {
                facilities[i] = new GameObject("Building_" + i + "_2x2").transform;
                facilities[i].SetParent(_root.transform);
                facilities[i].position = new Vector3(i * 2.6f, 0, 0);
                labels[i] = "施設" + i;
            }

            var gallery = _root.AddComponent<FacilityDesignGallery>();
            gallery.Configure(_camera, facilities, labels, labels);
            return gallery;
        }

        [Test]
        public void SelectWrapsAroundInBothDirections()
        {
            var gallery = Gallery(3);

            gallery.Select(-1);
            Assert.That(gallery.SelectedIndex, Is.EqualTo(2));

            gallery.Select(3);
            Assert.That(gallery.SelectedIndex, Is.EqualTo(0));
            Assert.That(gallery.SelectedLabel, Is.EqualTo("施設0"));
        }

        [Test]
        public void SelectShowsOnlyTheSelectedFacility()
        {
            var gallery = Gallery(3);

            gallery.Select(1);

            for (var i = 0; i < gallery.FacilityCount; i++)
            {
                Assert.That(gallery.GetFacility(i).gameObject.activeSelf, Is.EqualTo(i == 1));
            }
        }

        [Test]
        public void OverviewShowsEveryFacility()
        {
            var gallery = Gallery(3);
            gallery.Select(1);

            gallery.ShowOverview();

            for (var i = 0; i < gallery.FacilityCount; i++)
            {
                Assert.That(gallery.GetFacility(i).gameObject.activeSelf, Is.True);
            }
        }

        [Test]
        public void EmptyGalleryDoesNotThrow()
        {
            var gallery = Gallery(0);

            Assert.DoesNotThrow(() => gallery.Select(1));
            Assert.DoesNotThrow(() => gallery.ShowOverview());
            Assert.That(gallery.SelectedLabel, Is.Null);
        }
    }
}
