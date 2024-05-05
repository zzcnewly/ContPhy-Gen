import math
import numpy as np
import matplotlib.pyplot as plt
import os, json
import argparse


def plot(x,y, s = None, c=None, figure_name = None, range_x = None, range_y = None):
    if figure_name is not None:
        plt.figure(figure_name)
    plt.scatter(x, y, s=s, c=c)
    ax = plt.gca()
    if range_x is not None:
        ax.set_xlim(range_x)
    if range_y is not None:
        ax.set_ylim(range_y)
    plt.xlabel('X-axis')
    plt.ylabel('Y-axis')
    plt.title('2D Pixels')
    if figure_name is not None:
        plt.savefig(f'{figure_name}.jpg')
    else : plt.show()

def transform_point(P, parent_world_position2, parent_world_rotation2_mat):
    P_world2 = parent_world_position2 + parent_world_rotation2_mat.dot(P.T).T
    return P_world2

def euler_to_rotation_matrix(euler_angles):
    pitch = euler_angles[0]
    yaw = euler_angles[1]
    roll = euler_angles[2]
    Rx = np.array([[1, 0, 0], [0, np.cos(pitch), -np.sin(pitch)], [0, np.sin(pitch), np.cos(pitch)]])
    Ry = np.array([[np.cos(yaw), 0, np.sin(yaw)], [0, 1, 0], [-np.sin(yaw), 0, np.cos(yaw)]])
    Rz = np.array([[np.cos(roll), -np.sin(roll), 0], [np.sin(roll), np.cos(roll), 0], [0, 0, 1]])
    return Rz.dot(Ry.dot(Rx))

def WorldToLocal(aCamPos, aCamRot, aPos):  
    # aCamRot is euler angles and aPos can be 2D array with xyz on dim -1
    return np.linalg.inv(euler_to_rotation_matrix(np.deg2rad(aCamRot))).dot((aPos - aCamPos).T).T

def Project(aPos, aFov, aAspect):
    f = 1 / math.tan(aFov * math.pi / 180 * 0.5)
    f_ls = f / aPos[:, 2] # pos.z
    aPos[:, 0] = aPos[:, 0] * f_ls / aAspect
    aPos[:, 1] = aPos[:, 1] * f_ls
    return aPos

def ClipSpaceToViewport(aPos):
    aPos[:, 0] = aPos[:, 0] * 0.5 + 0.5
    aPos[:, 1] = aPos[:, 1] * 0.5 + 0.5
    return aPos
   
def WorldToViewport(aCamPos, aCamRot, aFov, aAspect, aPos):
    points = WorldToLocal(aCamPos, aCamRot, aPos)
    points = Project(points, aFov, aAspect)
    return ClipSpaceToViewport(points)
       
def WorldToImagePixelPos(aCamPos, aCamRot, aFov, aScrWidth, aScrHeight, aPos):
    # aCamRot is euler angles and aPos can be 2D array with xyz on dim -1
    p = WorldToViewport(aCamPos, aCamRot, aFov, aScrWidth / aScrHeight, aPos)
    p[:, 0] = p[:, 0] * aScrWidth
    p[:, 1] = p[:, 1] * aScrHeight
    return p
   
def WorldToGUIShowPos(aCamPos, aCamRot, aFov, aScrWidth, aScrHeight, aPos, toPlot = False):
    # aCamRot is euler angles and aPos can be 2D array with xyz on dim -1
    p = WorldToImagePixelPos(aCamPos, aCamRot, aFov, aScrWidth, aScrHeight, aPos)
    p[:, 1] = aScrHeight - p[:, 1]
    if toPlot:
        plot(p[:, 0], p[:, 1])
    return p

def get_rigid_information(loc, rot, scale):
    # loc: world position
    # rot: euler angles
    # scale: world scale
    # return- loc: world position, normal1, normal2: world vec, distance1: half scale right on normal1, distance2 alike
    loc = loc
    rotation_mat = euler_to_rotation_matrix(np.deg2rad(rot))
    normal1 = rotation_mat.dot(np.array([1, 0, 0]).T).T
    normal2 = rotation_mat.dot(np.array([0, 1, 0]).T).T
    distance1 = scale[1] / 2
    distance2 = scale[0] / 2
    return (loc, normal1, normal2, distance1, distance2)

def get_particles_raw_3d(data_folder, 
                         width = 1920, 
                         height = 1080, 
                         print_shape = False, 
                         visualize_result = False, 
                         visualize_frame = 100,
                         plot_out_path="./"):
    file_path_4d = os.path.join(data_folder, "outputs4D.json")
    file_path = os.path.join(data_folder, "outputs.json")
    if os.path.exists(file_path_4d):
        with open(file_path_4d, "r") as f:
            data = json.load(f)
    if os.path.exists(file_path):
        with open(file_path, "r") as f:
            data_meta = json.load(f)
    
    rigidData = data["rigidbody4D"]
    softData = data["softbody4D"]
    rigidbodyStatesStaticity = rigidData["rigidbodyStatesStaticity"]
    rigidbodyCentroidStates = rigidData["rigidbodyCentroidStates"]
    rigidbodyMeshVertices = rigidData["rigidbodyMeshVertices"]
    rigidbodyMeshFaces = rigidData["rigidbodyMeshFaces"]
    rigidbodyVoxelPosition = rigidData["rigidbodyVoxelPosition"]
    softbodyTrackedParticles = softData["softbodyTrackedParticles"]
    softbodyMeshVertices = softData["softbodyMeshVertices"]
    softbodyMeshFaces = softData["softbodyMeshFaces"]
    print("Data Loaded!")
    # print(rigidbodyCentroidStates.keys())
    # for key in rigidbodyCentroidStates:
        # print(key, np.array(rigidbodyCentroidStates[key]).shape)
    ##########################################################

    # get tracked particles
    object_frame_particles = {}
    object_frame_meshes = {}
    # add softbodies tracked particles and meshes for each frame
    frame_num = 0
    for key in softbodyTrackedParticles:  # per object
        object_frame_particles[key] = np.array(softbodyTrackedParticles[key])
        frame_meshes = []
        for i in range(len(softbodyMeshVertices[key])):
            frame_meshes.append({"vertex": np.array(softbodyMeshVertices[key][i]), "face": np.array(softbodyMeshFaces[key][i]).reshape((-1, 3))})
        frame_num = max(frame_num, len(softbodyTrackedParticles[key]))
        object_frame_meshes[key] = frame_meshes
    # add rigidbodies tracked particles and meshes for each frame
    for key in rigidbodyStatesStaticity:  # per object
        if rigidbodyStatesStaticity[key] == True:
            # the output frame number equals to softbody's active frame number
            object_frame_particles[key] = np.array([rigidbodyVoxelPosition[key][2:]]).repeat(frame_num, axis=0)
            frame_meshes = []
            for i in range(frame_num):
                frame_meshes.append({"vertex": np.array(rigidbodyMeshVertices[key]), "face": np.array(rigidbodyMeshFaces[key]).reshape((-1, 3))})
            object_frame_meshes[key] = frame_meshes
        else:
            voxels = rigidbodyVoxelPosition[key]
            mesh_vertices = rigidbodyMeshVertices[key]
            mesh_vertices_np = np.array(mesh_vertices)
            states = np.array(rigidbodyCentroidStates[key])
            temp = np.array(voxels)
            parent_world_position = temp[0]
            parent_world_rotation = temp[1]
            parent_world_rotation = np.deg2rad(parent_world_rotation)
            voxel_pos_world = temp[2:]
            parent_world_rotation_mat = euler_to_rotation_matrix(parent_world_rotation)
            parent_world_rotation_mat_inv = np.linalg.inv(parent_world_rotation_mat)
            this_voxels = []
            this_vertices = []
            for pos in states: # per frame
                parent_world_position2 = np.array(pos[0])
                parent_world_rotation2 = np.array(pos[1])
                parent_world_rotation2 = np.deg2rad(parent_world_rotation2)
                parent_world_rotation2_mat = euler_to_rotation_matrix(parent_world_rotation2)
                # per voxel
                P_local = parent_world_rotation_mat_inv.dot((voxel_pos_world - parent_world_position).T).T
                P_world_new = transform_point(P_local, parent_world_position2, parent_world_rotation2_mat)
                this_voxels.append(P_world_new)
                P_local_v = parent_world_rotation_mat_inv.dot((mesh_vertices_np - parent_world_position).T).T
                P_world_new_v = transform_point(P_local_v, parent_world_position2, parent_world_rotation2_mat)
                this_vertices.append(P_world_new_v)
            object_frame_particles[key] = np.array(this_voxels)
            frame_meshes = []
            for i in range(len(this_vertices)):
                frame_meshes.append({"vertex": np.array(this_vertices[i]), "face": np.array(rigidbodyMeshFaces[key]).reshape((-1, 3))})
            object_frame_meshes[key] = frame_meshes

    ##########################################################
    #  add particles to 2D screen
    object_frame_pixel_pos = {}
    cam = data_meta["outputCamera"][list(data_meta["outputCamera"].keys())[0]]
    cam_pos = np.array(cam["position"])
    cam_rot = np.array(cam["rotation"])
    cam_fov = cam["fov"][0]
    for key in object_frame_particles:  # per object
        points = object_frame_particles[key]
        points_shape = points.shape
        out = WorldToImagePixelPos(cam_pos, cam_rot, cam_fov, 
                                    width, height, 
                                    points.reshape((-1, 3)))
        out = np.around(out.reshape(points_shape)[:, : ,:2])
        object_frame_pixel_pos[key] = out

    ##########################################################
    # visualize mesh verts to 2D screen
    object_frame_pixel_vert = {}
    for key in object_frame_meshes:  # per object
        meshes = object_frame_meshes[key]
        out = []
        for i in range(frame_num):
            out0 = WorldToImagePixelPos(cam_pos, cam_rot, cam_fov, 
                                        width, height, 
                                        meshes[i]["vertex"].reshape((-1, 3)))
            out0 = np.around(out0[: ,:2])
            out.append(out0)
        object_frame_pixel_vert[key] = out


    # visualization the result
    if visualize_result:
        ps = np.zeros((0, 3))
        k= 0
        i_cl = list(range(len(object_frame_pixel_pos)))
        i_cl = np.array(i_cl)
        np.random.shuffle(i_cl)
        for key in object_frame_pixel_vert:  # per object
            points = object_frame_pixel_vert[key][visualize_frame]
            points = np.concatenate((points, i_cl[k] * np.ones((points.shape[0], 1))), axis=1)
            ps = np.concatenate((ps, points), axis=0)
            k+=1
        plot(ps[:, 0], ps[:, 1], 1, ps[:, 2], range_x=[0, width], range_y=[0, height])
        # visualize particles
        i_cl = list(range(len(object_frame_pixel_pos)))
        i_cl = np.array(i_cl)
        np.random.shuffle(i_cl)
        if not os.path.exists(plot_out_path):
            os.makedirs(plot_out_path)
        for frame in range(frame_num):
            ps = np.zeros((0, 3))
            k= 0
            for key in object_frame_pixel_pos:  # per object
                points = object_frame_pixel_pos[key][frame]
                points = np.concatenate((points, i_cl[k] * np.ones((points.shape[0], 1))), axis=1)
                ps = np.concatenate((ps, points), axis=0)
                k+=1
            plot(ps[:, 0], ps[:, 1], 1, ps[:, 2], 
                 range_x=[0, width], range_y=[0, height], 
                 figure_name=os.path.join(plot_out_path, str(frame)))
    ##########################################################
    if print_shape:
        for key in object_frame_particles:
            # per object->per frame->per voxel point->world xyz
            print("name:", key)
            print("3D space:", object_frame_particles[key].shape)
            print("2D pixel:", object_frame_pixel_pos[key].shape)
            if len(object_frame_meshes[key]) > 0:
                print("Mesh frames:", len(object_frame_meshes[key]))
                print("Mesh in the 1st  frame: vertices are", object_frame_meshes[key][0]["vertex"].shape, "| faces are", object_frame_meshes[key][0]["face"].shape)
                print("Mesh in the last frame: vertices are", object_frame_meshes[key][-1]["vertex"].shape, "| faces are", object_frame_meshes[key][-1]["face"].shape)
            else:
                print("No Mesh Output!")
            print("--------------------------------------")
    
    return object_frame_particles, object_frame_pixel_pos, object_frame_meshes, object_frame_pixel_vert


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--trial_path", type=str)  # data path
    parser.add_argument("--print_shapes", action='store_true')  
    parser.add_argument("--visualize_frame", action='store_true')  
    parser.add_argument("--plot_save_path", type=str, default='./')  # data path
    parser.add_argument("--selected_frame", type=int, default=20)  
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()
    get_particles_raw_3d(args.trial_path, 
                         print_shape = args.print_shapes, 
                         visualize_result = args.visualize_frame, 
                         visualize_frame = args.selected_frame,
                         plot_out_path=args.plot_save_path)