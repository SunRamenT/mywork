import pandas as pd
import matplotlib.pyplot as plt

df_lea01 = pd.read_csv('Learning.csv',index_col=0,header=0)
print(df_lea01.tail(3))

df_ans01 = pd.read_csv('Answer.csv',index_col=0,header=0)
print(df_ans01.tail(3))

from sklearn import tree

model_01 = tree.DecisionTreeClassifier(max_depth=4, random_state=0)
model_01.fit(df_lea01, df_ans01)

print(model_01.classes_)

df_lea01.columns = ['open', ' high', 'low', 'close']
from sklearn.tree import plot_tree
plot_tree (model_01, feature_names=df_lea01.columns, filled= True)
plt.show()