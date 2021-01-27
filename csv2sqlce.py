#! /usr/bin/env python3

import csv

FPATH = 'seeds orig.csv'

colnames = None
table_rows = []

with open(FPATH) as f:
	for row in csv.reader(f, delimiter=','):
		if colnames is None:
			colnames = row
			print("CREATE TABLE [seeds] (")
			for i, name in enumerate(colnames):
				b_is_last = len(colnames)-1 == i
				print("    [{}] NVARCHAR(50){}".format(name, "" if b_is_last else ","))
			print(");\n")
		else:
			table_rows.append(row)
			s = "INSERT INTO [seeds] ({}) VALUES (".format(','.join(['[{}]'.format(x) for x in colnames]))

			b_is_first = True
			for val in row:
				s += "{}'{}'".format("" if b_is_first else ", ", val)
				b_is_first = False
			s += ");"
			print(s)
