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
import networkx as nx
import numpy as np
import random
from typing import List, Tuple, Dict
from itertools import product


# ==============================
# 定数定義
# ==============================
DEFAULT_STATE_DIM = 3        # lambda2 + degree + clustering
DEFAULT_ACTION_DIM = 100
DEFAULT_ADD_DIM = 50
DEFAULT_LR = 1e-3
DEFAULT_GAMMA = 0.95


# 非連結が発生しないよう修正[8/18]
class ActorCriticNetwork(nn.Module):
    def __init__(self, state_dim: int, action_dim: int):
        super().__init__()
        self.shared = nn.Sequential(
            nn.Linear(state_dim, 64),
            nn.ReLU(),
            nn.Linear(64, 128),
            nn.ReLU()
        )
        self.actor = nn.Sequential(
            nn.Linear(128, 64),
            nn.ReLU(),
            nn.Linear(64, action_dim),
            nn.Softmax(dim=-1)
        )
        self.critic = nn.Sequential(
            nn.Linear(128, 64),
            nn.ReLU(),
            nn.Linear(64, 1)
        )

    def forward(self, state: torch.Tensor) -> Tuple[torch.Tensor, torch.Tensor]:
        x = self.shared(state)
        action_probs = self.actor(x)
        value = self.critic(x).squeeze(-1)
        return action_probs, value


class GraphAttackModel:
    def __init__(
        self,
        graph: nx.Graph,
        device: str = "cpu",
        state_dim: int = DEFAULT_STATE_DIM,
        action_dim: int = DEFAULT_ACTION_DIM,
        add_dim: int = DEFAULT_ADD_DIM,
        lr: float = DEFAULT_LR,
        gamma: float = DEFAULT_GAMMA,
    ):
        self.original_graph = graph.copy()
        self.current_graph = graph.copy()
        self.device = device

        self.state_dim = state_dim
        self.action_dim = action_dim
        self.add_dim = add_dim
        self.gamma = gamma

        self.model = ActorCriticNetwork(self.state_dim, self.action_dim).to(device)
        self.optimizer = optim.Adam(self.model.parameters(), lr=lr)

        self.last_state = None
        self.last_action_idx = None
        self.last_log_prob = None
        self.last_value = None
        self.best_sequence: List[Tuple[Tuple[int,int], Tuple[int,int]]] = []
        
    # ==========================================================
    # 変更点 1: ケメニー定数を計算するメソッドを追加
    # ==========================================================
    def _compute_kemeny_constant(self, G: nx.Graph) -> float:
        """グラフのケメニー定数を計算する"""
        if not nx.is_connected(G):
            # グラフが非連結の場合、ケメニー定数は無限大
            return float('inf')
        
        try:
            # ラプラシアン行列の固有値を取得 (0を含む)
            eigenvalues = nx.laplacian_spectrum(G)
            # 0を除いた正の固有値の逆数の和を計算
            # 浮動小数点誤差を考慮し、微小な値は0とみなす
            kemeny = sum(1.0 / val for val in eigenvalues if val > 1e-10)
            return kemeny
        except nx.NetworkXError:
            return float('inf') # 計算エラーの場合

    def _extract_node_features(self) -> Dict[int, List[float]]:
        return {
            node: [
                self.current_graph.degree(node),
                nx.clustering(self.current_graph, node),
            ]
            for node in self.current_graph.nodes()
        }

    # ==========================================================
    # 変更点 2: 状態計算をケメニー定数ベースに変更
    # ==========================================================
    def _compute_state(self) -> torch.Tensor:
        # 状態の第一要素を代数的連結性(lambda2)からケメニー定数に変更
        kemeny = self._compute_kemeny_constant(self.current_graph)
        
        node_features = self._extract_node_features()
        node_features_avg = np.mean(list(node_features.values()), axis=0)
        
        # 状態ベクトルを [ケメニー定数, 平均次数, 平均クラスタリング係数] にする
        state = np.concatenate([[kemeny], node_features_avg])
        return torch.tensor(state, dtype=torch.float32).to(self.device)

    def _generate_action_pool(self) -> List[Tuple[Tuple[int, int], Tuple[int, int]]]:
        existing_edges = list(self.current_graph.edges())
        non_edges = list(nx.non_edges(self.current_graph))

        bridge_edges = set(nx.bridges(self.current_graph))
        remove_candidates = [e for e in existing_edges if e not in bridge_edges]
        
        add_candidates = random.sample(non_edges, min(self.add_dim, len(non_edges)))
        action_pool = list(product(remove_candidates, add_candidates))

        if len(action_pool) > self.action_dim:
            action_pool = random.sample(action_pool, self.action_dim)
        return action_pool
    
    # ==========================================================
    # 変更点 3: 報酬計算をケメニー定数ベースに変更
    # ==========================================================
    def train(self, num_episodes: int, rollout_depth: int):
        candidates_per_episode = self.action_dim
        for episode in range(num_episodes):
            self.current_graph = self.original_graph.copy()
            
            # 初期状態のケメニー定数を計算
            initial_kemeny = self._compute_kemeny_constant(self.current_graph)
            if initial_kemeny == float('inf'): # 初期グラフが非連結ならスキップ
                continue

            self.action_pool = self._generate_action_pool()
            if not self.action_pool:
                continue

            best_sequence, best_reward = None, float("-inf")
            best_values, best_log_probs, best_states, best_next_states = [], [], [], []

            for _ in range(min(candidates_per_episode, len(self.action_pool))):
                temp_graph = self.original_graph.copy()
                sequence, values, log_probs, states, next_states = [], [], [], [], []

                for step in range(rollout_depth):
                    self.current_graph = temp_graph.copy()
                    state = self._compute_state()
                    action_probs, value = self.model(state)

                    action_dist = torch.distributions.Categorical(action_probs)
                    action_idx = action_dist.sample()
                    log_prob = action_dist.log_prob(action_idx)
                    action = self.action_pool[action_idx.item()]

                    if temp_graph.has_edge(*action[0]):
                        temp_graph.remove_edge(*action[0])
                    if not temp_graph.has_edge(*action[1]):
                        temp_graph.add_edge(*action[1])

                    self.current_graph = temp_graph.copy()
                    next_state = self._compute_state()

                    sequence.append(action)
                    log_probs.append(log_prob)
                    values.append(value)
                    states.append(state)
                    next_states.append(next_state)

                # 最終状態のケメニー定数を計算
                final_kemeny = self._compute_kemeny_constant(temp_graph)
                if final_kemeny == float('inf'): # 途中で非連結になった場合は低い報酬を与える
                    final_kemeny = initial_kemeny - 100 # ペナルティ

                # 報酬 = ケメニー定数の増加量 (最大化が目標)
                total_reward = final_kemeny - initial_kemeny

                if total_reward > best_reward:
                    best_reward = total_reward
                    best_sequence = sequence
                    best_log_probs = log_probs
                    best_values = values
                    best_states = states
                    best_next_states = next_states
                    self.best_sequence = best_sequence

            if best_sequence:
                self._update_model(best_sequence, best_reward, best_values, best_log_probs, best_next_states)

        return self

    def _update_model(self, sequence, reward, values, log_probs, next_states):
        # (_update_modelメソッドの内部は変更なし)
        T = len(sequence)
        actor_loss, critic_loss = 0.0, 0.0
        for t in range(T):
            r = reward / T
            target = r + self.gamma * self.model(next_states[t])[1].detach()
            advantage = target - values[t].detach()
            actor_loss += -log_probs[t] * advantage
            critic_loss += (values[t] - target) ** 2
        loss = actor_loss + critic_loss
        self.optimizer.zero_grad()
        loss.backward()
        self.optimizer.step()