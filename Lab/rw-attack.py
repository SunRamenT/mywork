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

from attacker import GraphAttackModel

import networkx as nx

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
               add_pattern, remove_pattern, attack_interval, graph_name):
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
            add_edge = 'ml'
        if remove_pattern == 1:
            remove_edge = 'ml'
        

    if attack_interval:
        label = str(name) + "-" + str(add_edge) + "-" + str(remove_edge)
    else:
        label = str(name)
    return f'{label:12}\t{n}\t{m}\t{graph_name}\t{count}\t{naborts}\t{nhittings}\t{c_avg:.0f}\t{c_conf:.0f}\t{h_avg:.0f}\t{h_conf:.0f}\t{mh_avg:.0f}\t{mh_conf:.0f}'


def simulate(agent_name,
             g,
             start_vertex=1,
             alpha=0,
             ntrials=100,
             attack_interval=None,
             add_pattern=None,
             remove_pattern=None,
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
            if agent.step > MAX_STEPS:
                naborts += 1
                break
    
            total_edges = len(list(g.edges()))
            
            
            if attack_interval is not None and agent.step % attack_interval == 0:
                #非連結になってない場合にのみ、攻撃する。非連結が目的なので、すでに非連結になっているなら、攻撃する意味がない
                if g.is_connected():
                    #if len(list(g.neighbors(agent.current))) > 0:
                    # Contracted Graph (CG) を生成
                    CG = nx.Graph()
                    for i in g.vertices():
                        CG.add_node(i)
                    for (i, j) in g.edges():
                        CG.add_edge(i, j)
                      
                    # グラフ攻撃モデルの訓練
                    attacker = GraphAttackModel(CG)
                    attacker.train(num_episodes=1, rollout_depth=1)
                  
                    # 3. best_sequenceの1個目だけ取る
                    first_action = attacker.best_sequence[0]
                    remove_edge = first_action[0]
                    add_edge = first_action[1]
                    
                    u, v = remove_edge
                    i, j = add_edge
                        
                    #print(g)
                    #print(u, v, i, j)
                    if g.has_edge(u, v):
                        g.delete_edge(u, v)
                    if not g.has_edge(i, j):
                        g.add_edge(i, j)

                    #print( u, v, i, j)
                    #print(g)
                # 攻撃後エッジの数が変わらないようにしている
                current_edges = len(list(g.edges()))
                if current_edges > total_edges:
                    u, v = random.choice(list(g.edges()))
                    #リンク削除
                    g.delete_edge(u, v)
                elif current_edges < total_edges:
                    u, v = random.sample(list(g.vertices()), 2)
                    #リンク追加
                    g.add_edge(u, v)
                else:
                    pass

                    
        # print("--" + str(g.is_connected()))
        
        stat = status_str(agent, g, count, naborts, covers, hittings,
                          mean_hittings, add_pattern, remove_pattern,
                          attack_interval, graph_name)
        print(stat + '\r', file=sys.stderr, end='')
    if not sys.stdout.isatty():
        print('', file=sys.stderr)
    print(stat + ' ')


def main():
    opt = getopts('s:N:n:k:a:g:A:y:z:') or usage()
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
                         attack_interval, add_pattern, remove_pattern, graph_types)


if __name__ == "__main__":
    main()