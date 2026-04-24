import cv2
import numpy as np
import datetime
import os

class SketchScanner:

    def __init__(self):

        self.sketches = {
            "0-1-2-3": {
                "id": 1,
                "name": "sketch1"
            },
            "4-5-6-7": {
                "id": 2,
                "name": "sketch2"
            },
            "8-9-10-11": {
                "id": 3,
                "name": "sketch3"
            },
            "12-13-14-15": {
                "id": 3,
                "name": "sketch4"
            },
            "16-17-18-19": {
                "id": 3,
                "name": "sketch5"
            }
        }

    def scan(self, image, output_dir):

        # Bild laden
        img = cv2.imread(image)
        if img is None:
            raise FileNotFoundError("Bild nicht gefunden!")

        # ArUco-Marker erkennen
        aruco_dict = cv2.aruco.getPredefinedDictionary(cv2.aruco.DICT_4X4_50)
        parameters = cv2.aruco.DetectorParameters()
        detector = cv2.aruco.ArucoDetector(aruco_dict, parameters)

        corners, ids, _ = detector.detectMarkers(img)
        if ids is None or len(ids) < 4:
            raise ValueError("Nicht alle 4 Marker gefunden!")

        # Marker sortieren: 0=TopLeft, 1=TopRight, 2=BottomRight, 3=BottomLeft
        ids = ids.flatten()
        marker_corners = {i: corners[idx][0] for idx, i in enumerate(ids)}

        # Sketch vorhanden?
        id_list = "-".join(str(i) for i in sorted(ids))
        if id_list in self.sketches:
            sketch = self.sketches[id_list]
        else:
            raise ValueError(f"Kein Sketch gefunden!")

        # Zielgröße in Pixel
        dpi = 300
        width_cm, height_cm = 25.2, 16.5
        width_px = int(width_cm / 2.54 * dpi)
        height_px = int(height_cm / 2.54 * dpi)

        # Quellpunkte = Mittelpunkte der Marker
        sorted_ids = sorted(ids)
        pts_src = np.array([
            marker_corners[sorted_ids[0]].mean(axis=0),
            marker_corners[sorted_ids[1]].mean(axis=0),
            marker_corners[sorted_ids[2]].mean(axis=0),
            marker_corners[sorted_ids[3]].mean(axis=0)
        ], dtype="float32")

        # Zielpunkte
        pts_dst = np.array([
            [0, 0],
            [width_px - 1, 0],
            [width_px - 1, height_px - 1],
            [0, height_px - 1]
        ], dtype="float32")

        # Perspective Transform
        M = cv2.getPerspectiveTransform(pts_src, pts_dst)
        cropped = cv2.warpPerspective(img, M, (width_px, height_px))

        timestamp = datetime.datetime.now().strftime("%Y-%m-%d-%H%M%S")

        # Dateinamen zusammensetzen
        filename = f"{timestamp}_{sketch['name']}.png"

        # Ergebnis speichern
        output_path = os.path.join(output_dir, filename)
        cv2.imwrite(output_path, cropped)