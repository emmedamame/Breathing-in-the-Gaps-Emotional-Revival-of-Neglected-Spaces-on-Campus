import cv2
import socket
import numpy as np
from pupil_apriltags import Detector

UDP_IP = "127.0.0.1"
UDP_PORT = 5005
CAM_INDEX = 0
TAG_SIZE = 0.08
FX, FY, CX, CY = 800.0, 800.0, 320.0, 240.0

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
detector = Detector(families="tag36h11")
cap = cv2.VideoCapture(CAM_INDEX)
cv2.namedWindow("AprilTag Camera", cv2.WINDOW_NORMAL)

while True:
    ok, frame = cap.read()
    if not ok:
        continue

    gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
    tags = detector.detect(
        gray,
        estimate_tag_pose=True,
        camera_params=(FX, FY, CX, CY),
        tag_size=TAG_SIZE,
    )

    if tags:
        for tag in tags:
            corners = tag.corners.astype(np.int32)
            for i in range(4):
                p1 = tuple(corners[i])
                p2 = tuple(corners[(i + 1) % 4])
                cv2.line(frame, p1, p2, (0, 255, 0), 2)
            c = tuple(tag.center.astype(np.int32))
            cv2.circle(frame, c, 4, (0, 0, 255), -1)
            cv2.putText(
                frame,
                f"id:{tag.tag_id}",
                (corners[0][0], corners[0][1] - 8),
                cv2.FONT_HERSHEY_SIMPLEX,
                0.6,
                (0, 255, 0),
                2,
            )

        R = tags[0].pose_R
        sy = np.sqrt(R[0, 0] * R[0, 0] + R[1, 0] * R[1, 0])
        if sy < 1e-6:
            x = np.arctan2(-R[1, 2], R[1, 1])
            y = np.arctan2(-R[2, 0], sy)
            z = 0
        else:
            x = np.arctan2(R[2, 1], R[2, 2])
            y = np.arctan2(-R[2, 0], sy)
            z = np.arctan2(R[1, 0], R[0, 0])

        euler_deg = np.degrees([x, y, z])
        msg = f"{euler_deg[0]:.2f},{euler_deg[1]:.2f},{euler_deg[2]:.2f}"
        sock.sendto(msg.encode("utf-8"), (UDP_IP, UDP_PORT))
        cv2.putText(
            frame,
            f"Euler X:{euler_deg[0]:.1f} Y:{euler_deg[1]:.1f} Z:{euler_deg[2]:.1f}",
            (20, 40),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.8,
            (255, 255, 0),
            2,
        )
    else:
        cv2.putText(
            frame,
            "No AprilTag",
            (20, 40),
            cv2.FONT_HERSHEY_SIMPLEX,
            0.8,
            (0, 0, 255),
            2,
        )

    cv2.imshow("AprilTag Camera", frame)

    if cv2.waitKey(1) & 0xFF == 27:
        break

cap.release()
cv2.destroyAllWindows()
