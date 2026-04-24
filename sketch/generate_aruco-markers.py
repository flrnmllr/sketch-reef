import cv2

aruco_dict = cv2.aruco.getPredefinedDictionary(cv2.aruco.DICT_4X4_50)

for marker_id in range(aruco_dict.bytesList.shape[0]):
    img = cv2.aruco.generateImageMarker(aruco_dict, marker_id, 1024)
    cv2.imwrite(f"aruco-markers/aruco-marker-{marker_id}.png", img)