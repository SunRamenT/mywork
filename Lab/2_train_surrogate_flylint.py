#!/usr/bin/env python3
#
# Compare performance of different RW stragtegies.
# Copyright (c) 2025, Taiyo Hirayama
# Make based on random walk by Hiroyuki Ohsaki.
# All rights reserved.
#
import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import Dataset, DataLoader
import json
import numpy as np
import networkx as nx

# --- モデルの定義 ---
class SurrogateGNN(nn.Module):
    """グラフからSLEMを予測するシンプルなGNN"""
    def __init__(self, feature_dim=1, hidden_dim=64):
        super().__init__()
        self.conv1 = nn.Linear(feature_dim, hidden_dim)
        self.conv2 = nn.Linear(hidden_dim, hidden_dim)
        self.relu = nn.ReLU()
        # グラフ全体の特徴を読み取り、最終的なSLEM値(1つの数値)を予測
        self.readout = nn.Sequential(
            nn.Linear(hidden_dim, 32),
            nn.ReLU(),
            nn.Linear(32, 1)
        )

    def forward(self, x, adj):
        # GCN (Graph Convolutional Network) の基本的な計算
        x = self.relu(self.conv1(adj @ x))
        x = self.relu(self.conv2(adj @ x))
        # 全ノードの特徴量の平均を取ってグラフ全体の特徴量とする (Global Mean Pooling)
        graph_feature = torch.mean(x, dim=0)
        return self.readout(graph_feature)

# --- データセットの定義 ---
class GraphDataset(Dataset):
    def __init__(self, dataset_file):
        with open(dataset_file, 'r') as f:
            self.data = json.load(f)

    def __len__(self):
        return len(self.data)

    def __getitem__(self, idx):
        graph_data = self.data[idx]
        nodes = graph_data['nodes']
        edges = graph_data['edges']
        slem = graph_data['slem']
        
        n = len(nodes)
        adj = np.zeros((n, n))
        for u, v in edges:
            adj[u, v] = 1
            adj[v, u] = 1
        
        # 正規化された隣接行列 (GCNでよく使われる)
        I = np.eye(n)
        adj_hat = adj + I
        D_hat = np.diag(np.sum(adj_hat, axis=1))
        D_hat_inv_sqrt = np.linalg.inv(np.sqrt(D_hat))
        norm_adj = D_hat_inv_sqrt @ adj_hat @ D_hat_inv_sqrt
        
        # ノード特徴量は単純に全ノードで「1」とする
        features = np.ones((n, 1))

        return {
            'adj': torch.FloatTensor(norm_adj),
            'features': torch.FloatTensor(features),
            'slem': torch.FloatTensor([slem])
        }

# --- 訓練の実行 ---
def train():
    dataset = GraphDataset('slem_dataset.json')
    dataloader = DataLoader(dataset, batch_size=1, shuffle=True) # バッチサイズは1
    
    model = SurrogateGNN()
    optimizer = optim.Adam(model.parameters(), lr=1e-4)
    criterion = nn.MSELoss() # 予測値と真の値の誤差(MSE)を最小化

    print("代理モデルの訓練を開始します...")
    for epoch in range(10): # エポック数は調整が必要
        total_loss = 0
        for data in dataloader:
            adj, features, slem_true = data['adj'][0], data['features'][0], data['slem'][0]
            
            optimizer.zero_grad()
            slem_pred = model(features, adj)
            loss = criterion(slem_pred.squeeze(), slem_true)
            loss.backward()
            optimizer.step()
            total_loss += loss.item()
        
        avg_loss = total_loss / len(dataloader)
        print(f"Epoch {epoch+1}, Average Loss: {avg_loss:.6f}")

    print("訓練が完了しました。'surrogate_model.pth'にモデルを保存します。")
    torch.save(model.state_dict(), 'surrogate_model.pth')

if __name__ == '__main__':
    train()