import os
import json
import shutil
from tqdm import tqdm
import argparse


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--origin", type=str)  # model path
    parser.add_argument("--output", type=str, default='')  # data path
    return parser.parse_args()


if __name__ == "__main__":
    args = parse_args()
    # global settings
    parent_folder_path = args.origin
    output_folder_path = args.output
    filename = "outputs.json"  # must have "captureSteps" "validity"

    print("Start Refining the dataset...")
    folders = [f for f in os.listdir(parent_folder_path) if os.path.isdir(os.path.join(parent_folder_path, f))]
    valid_folders = []
    this_valid_ = 0
    for i, folder_name in tqdm(enumerate(folders)):
        folder_path = os.path.join(parent_folder_path, folder_name)
        file_path = os.path.join(folder_path, filename)

        if os.path.exists(file_path):
            with open(file_path, "r") as f:
                data = json.load(f)

            if data.get("validity") == True:
                valid_folders.append((folder_name, this_valid_))
                this_valid_ += 1
        else:
            print(str(i) + ": no such ", file_path)

    for folder_name, i in tqdm(valid_folders):
        folder_path = os.path.join(parent_folder_path, folder_name)
        output_folder_path_i = os.path.join(output_folder_path, str(i))

        shutil.copytree(folder_path, output_folder_path_i)

    print("Valid folders number is: ", len(valid_folders))
