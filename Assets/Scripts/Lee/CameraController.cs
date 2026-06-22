using UnityEngine;

//플레이 모드에서 카메라를 자유롭게 조작하는 스크립트
//WASD: 이동, 마우스 우클릭 드래그: 회전, 스크롤: 줌

public class CameraController : MonoBehaviour
{

        [Header("이동 설정")]
        public float moveSpeed = 10f;       // WASD 이동 속도
        public float fastMoveSpeed = 30f;   // Shift 누를 때 빠른 이동 속도

        [Header("회전 설정")]
        public float rotateSpeed = 2f;      // 마우스 회전 감도

        [Header("줌 설정")]
        public float scrollSpeed = 5f;      // 스크롤 줌 속도

        // 내부 변수
        private float yaw = 0f;    // 좌우 회전각
        private float pitch = 0f;  // 상하 회전각

        void Start()
        {
            // 초기 회전값 저장
            yaw = transform.eulerAngles.y;
            pitch = transform.eulerAngles.x;
        }

        // Update is called once per frame
        void Update()
    {
        //마우스 우클릭 누르고 있을때만 회전
        if (Input.GetMouseButton(1))
        {
            // 마우스 이동량으로 회전각 계산
            yaw += Input.GetAxis("Mouse X") * rotateSpeed;
            pitch -= Input.GetAxis("Mouse Y") * rotateSpeed;

            // 상하 회전 제한 (-89 ~ 89도)
            pitch = Mathf.Clamp(pitch, -89f, 89f);

            // 카메라 회전 적용
            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }


            //이동 속도 결정(shif 누르면 빠르게)
            float speed = Input.GetKey(KeyCode.LeftShift) ? fastMoveSpeed : moveSpeed;

            // WASD 이동
            if (Input.GetKey(KeyCode.W))
                transform.position += transform.forward * speed * Time.deltaTime;
            if (Input.GetKey(KeyCode.S))
                transform.position -= transform.forward * speed * Time.deltaTime;
            if (Input.GetKey(KeyCode.A))
                transform.position -= transform.right * speed * Time.deltaTime;
            if (Input.GetKey(KeyCode.D))
                transform.position += transform.right * speed * Time.deltaTime;

            // 스크롤 줌
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            transform.position += transform.forward * scroll * scrollSpeed;
        }
}
