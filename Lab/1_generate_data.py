#!/usr/bin/env python3
#
# Compare performance of different RW stragtegies.
# Copyright (c) 2025, Taiyo Hirayama
# Make based on random walk by Hiroyuki Ohsaki.
# All rights reserved.
#

import networkx as nx
import numpy as np
import json
from tqdm import tqdm
import random

NUM_SAMPLES = 5000  # 生成するグラフの数
DATASET_FILE = 'slem_dataset.json'

def compute_slem(G: nx.Graph) -> float:
    """グラフのSLEM（絶対値第二固有値）を計算する (attacker_slem.pyから引用)"""
    if G.number_of_nodes() < 2 or not nx.is_connected(G):
        return 1.0
    try:
        L_norm = nx.normalized_laplacian_matrix(G, nodelist=sorted(G.nodes()))
        eigenvalues = np.linalg.eigvalsh(L_norm.toarray())
        lambda_2 = eigenvalues[1]
        slem = 1.0 - lambda_2
        return slem
    except Exception:
        return 1.0

def main():
    """データセットを生成するメイン関数"""
    dataset = []
    print(f"{NUM_SAMPLES}個のグラフデータを生成します...")

    for _ in tqdm(range(NUM_SAMPLES)):
        # 様々なサイズのグラフを生成
        n_nodes = random.randint(20, 100)
        p_edge = random.uniform(0.05, 0.2)
        
        # 連結グラフができるまで試行
        G = nx.gnp_random_graph(n_nodes, p_edge)
        while not nx.is_connected(G):
            G = nx.gnp_random_graph(n_nodes, p_edge)

        slem = compute_slem(G)
        
        # グラフ構造とSLEM値を保存
        data_entry = {
            'nodes': list(G.nodes()),
            'edges': list(G.edges()),
            'slem': slem
        }
        dataset.append(data_entry)

    print(f"データセットの生成が完了しました。{DATASET_FILE} に保存します。")
    with open(DATASET_FILE, 'w') as f:
        json.dump(dataset, f)

if __name__ == '__main__':
    main()