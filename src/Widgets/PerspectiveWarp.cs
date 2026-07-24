using UnityEngine;
using UnityEngine.UI;

namespace LCBridgeOverlay
{
    /// <summary>
    /// Эффект перспективы (кейстоун), как у игрового чата, «уходящего вдаль».
    /// Искажает вершины графики так, что сторона панели, ближняя к центру
    /// экрана (левая для правого оверлея), сужается — трапеция. Работает в
    /// ScreenSpaceOverlay (где обычной 3D-перспективы нет), пересчитывая каждую
    /// вершину в пространстве панели и сжимая её по мере удаления.
    /// </summary>
    public class PerspectiveWarp : BaseMeshEffect
    {
        public RectTransform Panel;
        public float Width = 340f;
        public float Strength = 0.16f;
        public bool Continuous; // для движущейся/масштабируемой графики (глаз) — пересчёт каждый кадр

        private void LateUpdate()
        {
            if (Continuous && graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || Panel == null || Strength <= 0f) return;
            var grt = (RectTransform)transform;
            var v = new UIVertex();
            int n = vh.currentVertCount;
            for (int i = 0; i < n; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                Vector3 world = grt.TransformPoint(v.position);
                Vector3 pp = Panel.InverseTransformPoint(world);
                // far: 0 у правого края (ближе к краю экрана), 1 у левого (к центру)
                float far = Mathf.Clamp01(-pp.x / Width);
                pp.y *= Mathf.Lerp(1f, 1f - Strength, far);            // сужение по вертикали
                pp.x *= Mathf.Lerp(1f, 1f - Strength * 0.35f, far);    // лёгкое схождение по горизонтали
                Vector3 world2 = Panel.TransformPoint(pp);
                v.position = grt.InverseTransformPoint(world2);
                vh.SetUIVertex(v, i);
            }
        }
    }
}
