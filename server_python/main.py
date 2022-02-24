import socket
import json
import numpy as np
import cv2
import queue
import time


HOST = '192.168.0.100'
PORT = 11000
Max_size = 65000
packs_info = None
frame = None
class_names = []
frame_data = None
CONFIDENCE_THRESHOLD = 0.2
NMS_THRESHOLD = 0.4
COLORS = [(0, 255, 255), (255, 255, 0), (0, 255, 0), (255, 0, 0)]
frame_buffer = queue.Queue()

net = cv2.dnn.readNet('model/yolov4-tiny.cfg','model/yolov4-tiny.weights')
#net = cv2.dnn.readNet('model/yolov4.cfg','model/yolov4.weights')
# # net.setPreferableBackend(cv2.dnn.DNN_BACKEND_CUDA)
# # net.setPreferableTarget(cv2.dnn.DNN_TARGET_CUDA_FP16)
model = cv2.dnn_DetectionModel(net)
model.setInputParams(size=(416, 416),scale=1/255,swapRB=True)
#model.setInputParams(size=(608, 608),scale=1/255,swapRB=True)

with open("model/coco_classes.txt", "r") as f:
    class_names = [cname.strip() for cname in f.readlines()]

s = socket.socket(socket.AF_INET,socket.SOCK_DGRAM)
s.bind((HOST, PORT))

print('server start at: %s:%s' % (HOST, PORT))
print('wait for connection...')


def img_decode(indata):
    _frame = np.frombuffer(indata, dtype=np.uint8)
    _frame = _frame.reshape(_frame.shape[0], 1)
    _frame = cv2.imdecode(_frame, cv2.IMREAD_COLOR)
    _frame = cv2.flip(_frame, 1)
    return _frame

while True:
    indata, addr = s.recvfrom(Max_size)
    print('recvfrom ' + str(addr) + ': ' + str(len(indata)))

    # check package is information or image data
    if len(indata) < 50:
        packs_info = json.loads(indata.decode())
        print(packs_info)

    else:
        indata_buffer = []
        indata_buffer.append(indata)

        if packs_info:
            package_size = packs_info['packs_num']
            if package_size == 1:
                frame = img_decode(indata_buffer[0])
                frame_buffer.put(frame)

            #combine the image data, whilch is oversize
            else:
                frame_data = None
                waiting_num = 0
                start = 0
                end = 0
                run_time = 0
                while True:
                    start = time.process_time()
                    indata, addr = s.recvfrom(Max_size)

                    if len(indata) > 50:
                        print('recvfrom2 ' + str(addr) + ': ' + str(len(indata)))
                        indata_buffer.append(indata)

                    if len(indata_buffer) == package_size:
                        for i in indata_buffer:
                            if frame_data is None:
                                frame_data = i
                            else:
                                frame_data += i
                        frame = img_decode(frame_data)
                        frame_buffer.put(frame)
                        break

                    end = time.process_time()
                    run_time += start-end
                    if run_time > 0.1 :
                        break

            #predict and send to client
            if frame_buffer.empty() is False and frame_buffer.qsize() > 1:
                try:
                    classes, scores, boxes = model.detect(frame_buffer.get(), CONFIDENCE_THRESHOLD, NMS_THRESHOLD)
                    for (classid, score, box) in zip(classes, scores, boxes):
                        color = COLORS[int(classid) % len(COLORS)]
                        label = "%s : %f" % (class_names[int(classid)], score)
                        cv2.rectangle(frame, box, color, 2)
                        cv2.putText(frame, label, (box[0], box[1] - 10), cv2.FONT_HERSHEY_SIMPLEX, 1, color, 2)
                        tempList = {"name":"box_info","box":box.tolist(),"label":label,"color":color}
                        json_str = json.dumps(tempList)

                        s.sendto(json_str.encode(), addr)
                        packs_info = None
                except:
                    print("pass")

            if frame is not None and type(frame) == np.ndarray:
                cv2.imshow("Stream", frame)
                if cv2.waitKey(1) & 0xFF == ord('q'):
                    break
