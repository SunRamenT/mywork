#!/usr/bin/env python3
#
# Compare performance of different RW stragtegies.
# Copyright (c) 2025, Taiyo Hirayama
# Make based on random walk by Hiroyuki Ohsaki.
# All rights reserved.
#

import collections
import math
import random
import statistics
import sys
from perlcompat import die, warn, getopts
import randwalk
import graph_tools
# import tbdump
import networkx as nx
import community

MAX_STEPS = 10000
GRAPH_TYPES = 'random,ba,ring,tree,lattice,voronoi'
AGENT_NAMES = 'SRW'


def usage():
    die(f"""\
usage: {sys.argv[0]} [-s #] [-N #] [-n #] [-k #] [-A #] [-y #] [-z #]
  -s seed            specifiy the seed of random number generator
  -N                 the number of simulation runs (default: 100)
  -n                 the desired number of vertices (default: 100)
  -k                 the desired average degree (default: 2.5)
  -a name[,name...]  list of agent names (default: {AGENT_NAMES})
  -g name[,name...]  list of graph types (default: {GRAPH_TYPES})
  -A step            perform adversary attack every STEP
  -y edges_remove    links remove
  -z edges_add       links add
  -D defense_interval   interval of defense agent
  -b d_edge_remove  the defense link remove
  -c d_edge_add     the defense link add
""")


def conf95(vals):
    """Return 95% confidence interval for measurements VALS."""
    zval = 1.960
    if len(vals) <= 1:
        return 0
    return zval * statistics.stdev(vals) / math.sqrt(len(vals))


def mean_and_conf95(vals):
    """Return the mean and the 95% confience interval for measurements
    VALS."""
    return statistics.mean(vals), conf95(vals)


def create_graph(type_, n=100, k=3.):
    """Randomly generate a graph instance using network generation model TYPE_.
    If possible, a graph with N vertices and the average degree of K is
    generated."""
    m = int(n * k / 2)
    g = graph_tools.Graph(directed=False)
    g.set_graph_attribute('name', type_)
    if type_ == 'random':
        return g.create_random_graph(n, m)
    if type_ == 'ba':
        return g.create_graph('ba', n, 10, int(k))
    if type_ == 'barandom':
        return g.create_graph('barandom', n, m)
    if type_ == 'ring':
        return g.create_graph('ring', n)
    if type_ == 'tree':
        return g.create_graph('tree', n)
    if type_ == 'btree':
        return g.create_graph('btree', n)
    if type_ == 'lattice':
        return g.create_graph('lattice', 2, int(math.sqrt(n)))
    if type_ == 'voronoi':
        return g.create_graph('voronoi', n // 2)
    if type_ == 'db':
        # NOTE: average degree must be divisable by 2.
        return g.create_graph('db', n, n * 2)
    if type_ == '3-regular':
        return g.create_random_regular_graph(n, 3)
    if type_ == '4-regular':
        return g.create_random_regular_graph(n, 4)
    if type_ == 'li_maini':
        # NOTE: 5 clusters, 5% of vertices in each cluster, other vertices are
        # added with preferential attachment.
        return g.create_graph('li_maini', int(n * .75), 5, int(n * .25 / 5))
    # FIXMME: support treeba, general_ba, and latent.
    assert False


def header_str():
    return '# agent    \tN\tM\ttype\tcount\tabort\tC\t95%\tH\t95%\tE[H]\t95%'


def status_str(agent, g, count, naborts, covers, hittings, mean_hittings,
               add_pattern, remove_pattern, attack_interval, d_add_pattern,
               d_remove_pattern, defense_interval, graph_name):
    name = agent.name()
    try:
        alpha = agent.alpha
    except AttributeError:
        alpha = None
    n = g.nvertices()
    m = g.nedges()
    type_ = g.get_graph_attribute('name')
    # Collect statistics.
    cover = agent.step
    covers.append(cover)
    # NOTE: hiting[v] records the hitting time at vertex V.
    hitting = agent.hitting[agent.target]
    
    # add by han
    if hitting == 0:
        hitting = 10000
    hittings.append(hitting)
    nhittings = hittings.count(10000)
    
    mean_hitting = statistics.mean(agent.hitting.values())
    mean_hittings.append(mean_hitting)
    c_avg, c_conf = mean_and_conf95(covers)
    h_avg, h_conf = mean_and_conf95(hittings)
    mh_avg, mh_conf = mean_and_conf95(mean_hittings)

    if attack_interval:
        if add_pattern == 1:
            add_edge = 'tri'
        elif add_pattern == 2:
            add_edge = 'loop'
        else:
            add_edge = 'cluster'

        if remove_pattern == 1:
            remove_edge = 'hub'
        elif remove_pattern == 2:
            remove_edge = 'exit'
        else:
            remove_edge = 'bridge'     

    
    if attack_interval:
        label = str(name) + "-" + str(add_edge) + "-" + str(remove_edge)
        if defense_interval:
            label = str(name) + "-D" + "-" + str(add_edge) + "-" + str(remove_edge)
    else:
        label = str(name)
    return f'{label:12}\t{n}\t{m}\t{graph_name}\t{count}\t{naborts}\t{nhittings}\t{c_avg:.0f}\t{c_conf:.0f}\t{h_avg:.0f}\t{h_conf:.0f}\t{mh_avg:.0f}\t{mh_conf:.0f}'


def karger_min_cut(g):

    super_nodes = {}
    for i in g.vertices():
        super_nodes[i] = []
    
    # Contracted Graph (CG) を生成
    CG = nx.Graph()
    for i in g.vertices():
        CG.add_node(i)
    for (i, j) in g.edges():
        CG.add_edge(i, j)

    # print(g)
    # Continue contracting the graph until there are only 2 nodes left
    while len(CG.nodes) > 2:
        # Select a random edge
        u, v = random.choice(list(CG.edges))
        # print(u, v)

        super_nodes[u].append(v)
        # 回帰的法
        if super_nodes[v]:
            super_nodes[u].extend(super_nodes[v])
            super_nodes[v] = []
        
        # Contract the nodes u and v
        CG = nx.contracted_nodes(CG, u, v, self_loops=False)

        # Remove self-loops
        self_loops = [(node, node) for node in CG.nodes if CG.has_edge(node, node)]
        CG.remove_edges_from(self_loops)

        # print(CG.nodes)

    first_nodes = int(list(CG.nodes)[0])
    second_nodes = int(list(CG.nodes)[1])
    super_nodes[first_nodes].append(first_nodes)
    super_nodes[second_nodes].append(second_nodes)

    # print(super_nodes)
    for i in super_nodes[first_nodes]:
        for j in super_nodes[second_nodes]:
            if g.has_edge(i, j):
                return i, j


# 三角法 == 1、始点法  == 2
def attack_agent_add(agent, add_pattern):
    g = agent.graph
    u = agent.current
    path = sorted(set(agent.path), key=agent.path.index)

    start_node = 1
    # 三角法  (できるだけ訪問中のエッジ付近にトライアングルを作る)
    if add_pattern == 1:
        pairs = []
        for v in g.neighbors(u):
            for w in g.neighbors(v):
                if w != u and not g.has_edge(u, w):
                    pairs.append((u, w))
        if pairs:
            # find the pair (u, w) with the smallest sum of degrees d(u) + d(w)
            u, w = min(pairs, key=lambda pair: g.degree(pair[0]) + g.degree(pair[1]))
            return g.add_edge(u, w)

    # 始点法
    elif add_pattern == 2:
        # 現在訪問中のノードと最初のノードが繋がっていないならば、繋く。
        if not g.has_edge(u, start_node):
            # そうしないと始点ノードが自己ループを持ってしまうから
            if u != 1:
                return g.add_edge(u, 1)
        else:
            """ 現在訪問中のノードと、これまで訪問した各ノード間でエッジのない箇所
            をみつけ、エッジを追加する。"""
            for p in path:
                if g.has_edge(u, p):
                    continue
                else:
                    return g.add_edge(u, p)
            """これまで、訪問した各ノード間と繋がっていないれば、何もしない。始点
           法の攻撃が成功と見なす。これが正しい方法。ランダムに追加と
           かはできない"""

    # クラスタ法 S
    elif add_pattern == 3:
        nx_graph = nx.Graph()
        for i in g.vertices():
            nx_graph.add_node(i)
        for (i, j) in g.edges():
            nx_graph.add_edge(i, j)

        # clustering
        partition = community.best_partition(nx_graph)
        clusters = {}
        for node, cluster_id in partition.items():
            if cluster_id not in clusters:
                clusters[cluster_id] = []
            clusters[cluster_id].append(node)

        # Find minimum id of cluster size
        smallest_cluster_size = 100
        smallest_cluster_id = 100
        for cluster_id, nodes in clusters.items():
            cluster_size = len(nodes)                                                             
            if cluster_size < smallest_cluster_size and cluster_size >= 2:
                smallest_cluster_id = cluster_id
                smallest_cluster_size = cluster_size
        if smallest_cluster_id != 100 and smallest_cluster_size != 100:
            random_pairs = random.sample(clusters[smallest_cluster_id], k=2)
            g.add_edge(random_pairs[0], random_pairs[1])
    # Lolipop :
    else:
        group_1 = list(g.vertices())[:60]
        group_2 = list(g.vertices())[60:]

        if not g.has_edge(group_1[-1], group_2[0]):
            g.add_edge(group_1[-1], group_2[0])
        else:
            for i in group_1:
                for j in group_1:
                    if i != j and not g.has_edge(i, j):
                        g.add_edge(i, j)
                        break
    return True

 
# ハブ次数減少法 == 1、進路遮断法 == 2
def attack_agent_remove(agent, remove_pattern):

    g = agent.graph
    u = agent.current

    # ハブ次数減少法: ハブノードの次数を低下させる
    if remove_pattern == 1:
        D_lists = {i: g.degree(i) for i in g.vertices()}
        u = max(D_lists, key=D_lists.get)
        v = max(g.neighbors(u), key=g.degree)
        g.delete_edge(u, v)

    # 進路遮断法: 非訪問ノードへのリンクを排除するる
    elif remove_pattern == 2:
        # 現在訪問中のノードに接続しているノードの集合
        neighbors_u = g.neighbors(u)

        k_u = {}
        for n_u in neighbors_u:
            k_u[n_u] = set()

        # 各隣接ノードでから k ホップ内に存在する未訪問ノードの数を数える
        k_hop = 2
        for n_u in neighbors_u:
            for j in g.vertices():
                if g.shortest_path_length(n_u, j) <= k_hop:
                    if j not in agent.path:
                        k_u[n_u].add(j)

        # 隣接ノードが存在する場合のみ、エッジの削除を行う
        if k_u:
            max_key = max(k_u, key=lambda x: len(k_u[x]))
            max_size = len(k_u[max_key])
            # k ホップ以内に未訪問が存在しない状況がある
            if max_size != 0:
                g.delete_edge(max_key, u)
    elif remove_pattern == 3:
        
        u, v = karger_min_cut(g)
        g.delete_edge(u, v)
    # Lolipop :
    else:
        group_1 = list(g.vertices())[:60]
        group_2 = list(g.vertices())[60:]
        finished = False

        for i in group_1:
            for j in group_2:
                if g.has_edge(i, j) and (i != group_1[-1] or j != group_2[0]):
                    g.delete_edge(i, j)
                    finished = True
                    if not g.is_connected():
                        g.add_edge(i, j)
                        finished = False

        if finished:
            for i in group_2:
                if g.degree(i) > 2:
                    for n in g.neighbors(i):
                        g.delete_edge(i, n)
                        break
    return True


# 防御法は今一種類だけ
def defense_agent_add(agent, d_add_pattern):
    g = agent.graph
    u = agent.current
    path = sorted(set(agent.path), key=agent.path.index)
    start_node = 1

    # エッジの追加： 次数1本のノード数を減らすことで、グラフが分断され
    # る未来を避けれる。次数が高いノードと次数が 1 のノードの間にエッ
    # ジを追加する (中心への近道を作る)

    if d_add_pattern == 1:
        
        nodes_degree = {}
        for i in g.vertices():
            nodes_degree[i] = g.degree(i)

    # print(nodes_degree)
    # Find the node with the smallest and largest degree
    #min_node = min(nodes_degree, key=nodes_degree.get)
    #max_node = max(nodes_degree, key=nodes_degree.get)
    #g.add_edge(min_node, max_node)
    #return True

        min_nodes = sorted(nodes_degree, key=nodes_degree.get)
        first_min_node = min_nodes[0]
        second_min_node = min_nodes[1]
        g.add_edge(first_min_node, second_min_node)
    return True
    

def defense_agent_remove(agent, d_remove_pattern):
    g = agent.graph
    u = agent.current
    path = sorted(set(agent.path), key=agent.path.index)
    start_node = 1
    
    # エッジの削除：過去訪問したノード間のエッジを削除することで、過去
    # に戻れないようにする。SRW の動作や  NBRW や SARW の動作のように
    # する。はメモリを使う == エッジを操作する
    if d_remove_pattern == 1:
        if not g.has_edge(u, start_node):
            # そうしないと始点ノードが自己ループを持ってしまうから
            if u != 1:
                g.delete_edge(u, 1)
            else:
                """ 現在訪問中のノードと、これまで訪問した各ノード間でエッジのない箇所
                をみつけ、エッジを追加する。"""
                for p in path:
                    #print(p)
                    if not g.has_edge(u, p):
                        continue
                    else:
                        g.delete_edge(u, p)
                        """これまで、訪問した各ノード間と繋がっていないれば、何もしない。始点
                        法の攻撃が成功と見なす。これが正しい方法。ランダムに追加と
                        かはできない"""

    elif d_remove_pattern == 2:
        g.delete_edge(u,path[-2])

        
    return True

    




def simulate(agent_name,
             g,
             start_vertex=1,
             alpha=0,
             ntrials=100,
             attack_interval=None,
             add_pattern=None,
             remove_pattern=None,
             defense_interval=None,
             d_add_pattern=None,
             d_remove_pattern=None,
             graph_types=None
             ):
    covers = []
    hittings = []
    mean_hittings = []
    naborts = 0

    original_graph = g.copy_graph()  # ここを追加
    graph_name = graph_types
    
    for count in range(1, ntrials + 1):

        g = original_graph.copy_graph()   # ここを追加
        #print(g)
        
        # Create an agent of a given agent name.
        cls = eval('randwalk.' + agent_name)
        agent = cls(graph=g,
                    current=start_vertex,
                    target=g.nvertices(),
                    alpha=alpha)
        # Perform an instance of simulation.
        while agent.ncovered < g.nvertices():
            agent.advance()
            # 非連結になっている場合 +1 足す
            # if not g.is_connected() or agent.step > MAX_STEPS:
            if agent.step > MAX_STEPS:
                naborts += 1
                break

            total_edges = len(list(g.edges()))
            if attack_interval is not None and agent.step % attack_interval == 0:
                # 非連結になってない場合にのみ、攻撃する。非連結が目的なので、すねに非連結になっているなら、攻撃する意味がない
                if g.is_connected():
                    if len(list(g.neighbors(agent.current))) > 0:
                        # 攻撃する
                        attack_agent_add(agent, add_pattern)
                        attack_agent_remove(agent, remove_pattern)

                        # 攻撃者がエッジの数が変らないようにしている
                        current_edges = len(list(g.edges()))
                        if current_edges > total_edges:
                            u, v = random.choice(list(g.edges()))
                            g.delete_edge(u, v)
                        elif current_edges < total_edges:
                            u, v = random.sample(list(g.vertices()), 2)
                            g.add_edge(u, v)
                        else:
                            pass

            if defense_interval is not None and agent.step % defense_interval == 0:
                # 防御する
                defense_agent_add(agent, d_add_pattern)
                defense_agent_remove(agent, d_remove_pattern)
                current_edges = len(list(g.edges()))
                if current_edges > total_edges:
                    u, v = random.choice(list(g.edges()))
                    g.delete_edge(u, v)

        # print("--" + str(g.is_connected()))
        # print(g)
        stat = status_str(agent, g, count, naborts, covers, hittings,
                          mean_hittings, add_pattern, remove_pattern,
                          attack_interval, defense_interval,
                          d_add_pattern, d_remove_pattern, graph_name)
        print(stat + '\r', file=sys.stderr, end='')
    if not sys.stdout.isatty():
        print('', file=sys.stderr)
    print(stat + ' ')


def main():
    opt = getopts('s:N:n:k:a:g:A:y:z:D:b:c:') or usage()
    seed = int(opt.s) if opt.s else None
    random.seed(seed)
    ntrials = int(opt.N) if opt.N else 100
    n_desired = int(opt.n) if opt.n else 100
    k_desired = float(opt.k) if opt.k else 3
    agent_names = opt.a if opt.a else AGENT_NAMES
    graph_types = opt.g if opt.g else GRAPH_TYPES
    attack_interval = int(opt.A) if opt.A else None
    add_pattern = int(opt.y) if opt.y else None
    remove_pattern = int(opt.z) if opt.z else None
    #defense = str(opt.D) if opt.D else None
    defense_interval = int(opt.D) if opt.D else None
    d_add_pattern = int(opt.b) if opt.b else None
    d_remove_pattern = int(opt.c) if opt.c else None

    
    start_vertex = 1
    for type_ in graph_types.split(','):

        if graph_types == "football":
            g = graph_tools.Graph(directed=False)
            for i in range(1,101):
                g.add_vertex(i)

            #print(g.vertices())    
            with open('football_data/football.gml', 'r') as f:
                lines = f.readlines()[1:]
                gml_data = ''.join(lines)
            football_graph = nx.parse_gml(gml_data)
            node_ids = list(football_graph.nodes)
            #print(node_ids)
            for edge in football_graph.edges():

                if node_ids.index(edge[0]) in g.nodes():
                    g.add_vertex(node_ids.index(edge[0]))
                    
                g.add_edge(node_ids.index(edge[0]),node_ids.index(edge[1]))   
        else:
            g = create_graph(type_, n_desired, k_desired)
        for agent in agent_names.split(','):
            alphas = [0]
            if agent in 'BiasedRW':
                alphas = [-0.5]
            for alpha in alphas:
                simulate(agent, g, start_vertex, alpha, ntrials,
                         attack_interval, add_pattern, remove_pattern,
                         defense_interval, d_add_pattern, d_remove_pattern,  graph_types)


if __name__ == "__main__":
    main()
