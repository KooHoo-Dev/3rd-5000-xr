// ─────────────────────────────────────────────
// Respawnable — 바닥 아래로 떨어지거나 너무 멀리 날아간 물체를 시작 자세로 되돌린다
// 학습 포인트
//  - 던지기 실습에서 물체가 월드 밖으로 사라지면 수업이 끊기므로 시작 자세를 기억해 두고 복귀시킵니다
//  - 손에 잡힌 동안은 되돌리지 않습니다 (XRGrabInteractable.isSelected 로 확인)
//  - 보간(Interpolate)을 켠 Rigidbody 는 transform 을 옮겨도 물리가 그 값을 버립니다. Rigidbody.position 으로 옮기고 다음 물리 스텝 직전에 한 번 더 옮깁니다 (세션 YF)
// 조작
//  - 없음
// ─────────────────────────────────────────────
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace StudyXR.Common
{
    public class Respawnable : MonoBehaviour
    {
        [SerializeField] private float killHeight = -5f;
        [SerializeField] private float maxDistance = 30f;
        [SerializeField] private float respawnDelay = 1f;

        private Rigidbody body;
        private XRGrabInteractable grab;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private float outsideTime;

        // 되돌리기를 다음 FixedUpdate 에서 마무리해야 하는지, 그동안 잠시 꺼 둔 보간 값
        private bool pending;
        private RigidbodyInterpolation savedInterpolation;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            grab = GetComponent<XRGrabInteractable>();
            SetHome(transform.position, transform.rotation);
        }

        public void SetHome(Vector3 pos, Quaternion rot)
        {
            homePosition = pos;
            homeRotation = rot;
        }

        // 왜 transform 만 옮기면 안 되는가 (세션 YF — "공이 번쩍이며 제자리로 안 돌아옴"의 원인)
        //  보간을 켠 Rigidbody 는 매 프레임 '지난 두 물리 스텝 자세 사이'를 transform 에 덮어씁니다.
        //  Update 에서 transform 만 옮기면, 다음 프레임에 물리 스텝이 없을 때(화면 fps > 물리 72 Hz) 보간이 옛 자리로 되돌려
        //  공이 한 프레임 집에 번쩍 보였다가 원래 자리로 돌아갑니다. 1초마다 다시 시도하므로 계속 번쩍입니다.
        //  그래서 (1) 보간을 잠시 끄고 (2) Rigidbody.position 으로 물리 자세를 바로 옮기고 (3) 다음 FixedUpdate 에서 한 번 더 옮긴 뒤 보간을 되돌립니다.
        public void RespawnNow()
        {
            outsideTime = 0f;
            if (body != null && !pending)
            {
                savedInterpolation = body.interpolation;
                body.interpolation = RigidbodyInterpolation.None;
                pending = true;
            }
            Teleport();
        }

        private void Teleport()
        {
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.position = homePosition;
                body.rotation = homeRotation;
            }
            transform.SetPositionAndRotation(homePosition, homeRotation);
        }

        // 물리 스텝 직전 : 같은 프레임에 다른 스크립트가 속도를 넣었어도(놓는 순간 대입 등) 여기서 0 으로 시작합니다
        private void FixedUpdate()
        {
            if (!pending) return;
            pending = false;
            body.interpolation = savedInterpolation;

            // 그사이에 잡혔거나 키네마틱이 됐으면(발사기 충전) 그쪽이 공을 움직이므로 손대지 않습니다
            bool held = grab != null && grab.isSelected;
            if (held || body.isKinematic) return;
            Teleport();
        }

        private void OnDisable()
        {
            if (!pending) return;
            pending = false;
            if (body != null) body.interpolation = savedInterpolation;
        }

        private void Update()
        {
            bool held = grab != null && grab.isSelected;
            bool outside = transform.position.y < killHeight || (transform.position - homePosition).sqrMagnitude > maxDistance * maxDistance;

            // 잠깐 벗어났다 돌아오는 경우(높이 던지기)를 거르려고 delay 동안 계속 벗어나 있을 때만 되돌립니다
            if (held || !outside)
            {
                outsideTime = 0f;
                return;
            }

            outsideTime += Time.deltaTime;
            if (outsideTime >= respawnDelay) RespawnNow();
        }
    }
}
