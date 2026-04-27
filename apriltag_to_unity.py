import cv2
import json
import math
import socket
import numpy as np
from pupil_apriltags import Detector

# ==================================================
# AprilTag 参数
# Tag Family = tag36h11
# Tag ID = 0
# Tag Size = 80 mm = 0.08 m
# ==================================================
TAG_FAMILY = "tag36h11"
TARGET_TAG_ID = 0
TAG_SIZE_METERS = 0.08

# ==================================================
# 摄像头参数
# 打不开摄像头时改成 1 或 2
# ==================================================
CAMERA_INDEX = 0

# ==================================================
# Unity UDP 参数
# ==================================================
UNITY_IP = "127.0.0.1"
UNITY_PORT = 5052

# ==================================================
# 运动参数
# POSITION_SCALE 越大，Unity 物体移动越明显
# SMOOTHING 越大跟随越快，越小越平滑
# ==================================================
POSITION_SCALE = 4.0
SMOOTHING = 0.35

# 相对位置模式：True 以首次看到 AprilTag 的位置为原点
USE_RELATIVE_POSITION = True

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

detector = Detector(
    families=TAG_FAMILY,
    nthreads=4,
    quad_decimate=1.0,
    quad_sigma=0.0,
    refine_edges=1,
    decode_sharpening=0.25,
    debug=0
)

cap = cv2.VideoCapture(CAMERA_INDEX)

if not cap.isOpened():
    raise RuntimeError("摄像头打不开。请把 CAMERA_INDEX 改成 1 或 2 后重新运行。")

last_pos = None
last_rot = None
origin_pos = None


def rotation_matrix_to_euler_xyz(R):
    sy = math.sqrt(R[0, 0] * R[0, 0] + R[1, 0] * R[1, 0])
    singular = sy < 1e-6

    if not singular:
        x = math.atan2(R[2, 1], R[2, 2])
        y = math.atan2(-R[2, 0], sy)
        z = math.atan2(R[1, 0], R[0, 0])
    else:
        x = math.atan2(-R[1, 2], R[1, 1])
        y = math.atan2(-R[2, 0], sy)
        z = 0

    return math.degrees(x), math.degrees(y), math.degrees(z)


print("==========================================")
print("AprilTag -> Unity UDP 已启动")
print("Tag Family:", TAG_FAMILY)
print("Target Tag ID:", TARGET_TAG_ID)
print("Tag Size:", TAG_SIZE_METERS, "meters")
print("Unity UDP:", UNITY_IP, UNITY_PORT)
print("按 q 退出 | 按 r 重新设置原点")
print("==========================================")


while True:
    ok, frame = cap.read()

    if not ok:
        print("读取摄像头失败")
        break

    h, w = frame.shape[:2]
    gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)

    # 简易相机内参估计（课堂演示用，有标定数据可替换）
    fx = w
    fy = w
    cx = w / 2.0
    cy = h / 2.0

    results = detector.detect(
        gray,
        estimate_tag_pose=True,
        camera_params=(fx, fy, cx, cy),
        tag_size=TAG_SIZE_METERS
    )

    selected_tag = None
    for tag in results:
        if int(tag.tag_id) == TARGET_TAG_ID:
            selected_tag = tag
            break

    if selected_tag is not None:
        tag = selected_tag

        tx = float(tag.pose_t[0][0])
        ty = float(tag.pose_t[1][0])
        tz = float(tag.pose_t[2][0])

        # 摄像头坐标 -> Unity 坐标
        raw_pos = np.array([tx, -ty, tz], dtype=float)

        if USE_RELATIVE_POSITION:
            if origin_pos is None:
                origin_pos = raw_pos.copy()
                print("已设置 AprilTag 初始位置为原点:", origin_pos)

            unity_pos = (raw_pos - origin_pos) * POSITION_SCALE
        else:
            unity_pos = raw_pos * POSITION_SCALE

        rx, ry, rz = rotation_matrix_to_euler_xyz(tag.pose_R)

        # 调整旋转方向以符合 Unity 习惯
        unity_rot = np.array([rx, -ry, -rz], dtype=float)

        if last_pos is None:
            last_pos = unity_pos
            last_rot = unity_rot
        else:
            last_pos = last_pos * (1.0 - SMOOTHING) + unity_pos * SMOOTHING
            last_rot = last_rot * (1.0 - SMOOTHING) + unity_rot * SMOOTHING

        data = {
            "visible": True,
            "id": int(tag.tag_id),
            "x": float(last_pos[0]),
            "y": float(last_pos[1]),
            "z": float(last_pos[2]),
            "rx": float(last_rot[0]),
            "ry": float(last_rot[1]),
            "rz": float(last_rot[2])
        }

        sock.sendto(json.dumps(data).encode("utf-8"), (UNITY_IP, UNITY_PORT))

        # 画 AprilTag 边框和中心点
        corners = tag.corners.astype(int)
        for i in range(4):
            p1 = tuple(corners[i])
            p2 = tuple(corners[(i + 1) % 4])
            cv2.line(frame, p1, p2, (0, 255, 0), 2)

        center = tuple(tag.center.astype(int))
        cv2.circle(frame, center, 5, (0, 0, 255), -1)

        cv2.putText(
            frame,
            f"ID:{tag.tag_id} X:{last_pos[0]:.2f} Y:{last_pos[1]:.2f} Z:{last_pos[2]:.2f}",
            (20, 40),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.7,
            (0, 255, 0),
            2
        )

        cv2.putText(
            frame,
            "Detected tag36h11 ID 0",
            (20, 75),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.7,
            (0, 255, 0),
            2
        )

    else:
        data = {"visible": False}
        sock.sendto(json.dumps(data).encode("utf-8"), (UNITY_IP, UNITY_PORT))

        cv2.putText(
            frame,
            "No target AprilTag detected",
            (20, 40),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.7,
            (0, 0, 255),
            2
        )

    cv2.imshow("AprilTag Camera - q quit / r reset origin", frame)

    key = cv2.waitKey(1) & 0xFF

    if key == ord("q"):
        break

    if key == ord("r"):
        origin_pos = None
        last_pos = None
        last_rot = None
        print("已重置原点。请把 AprilTag 放到新的初始位置。")

cap.release()
cv2.destroyAllWindows()
sock.close()
