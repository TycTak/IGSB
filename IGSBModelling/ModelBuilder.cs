using IGSBShared;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.FastTree;
using Microsoft.ML.Trainers.LightGbm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using static IGSBShared.Delegates;

namespace IGSBModelling
{
    public static class Ex
    {
        public static IEnumerable<IEnumerable<T>> DifferentCombinations<T>(this IEnumerable<T> elements, int k)
        {
            var retval = (k == 0 ? new[] { new T[0] } : elements.SelectMany((e, i) => elements.Skip(i + 1).DifferentCombinations(k - 1).Select(c => (new[] { e }).Concat(c))));
            return retval;
        }
    }

    public class ModelBuilder
    {
        static public event Message M;
        static public event ConfirmText C;
        static public event BreakProcess P;

        private MLContext mlContext = new MLContext(seed: 1);
        private IDataView trainingDataView;
        private DataViewSchema predictionPipelineSchema;
        private IEstimator<ITransformer> trainingPipeline;
        private List<Metric> metricList;

        public class Metric
        {
            public double MAE { get; set; }
            public double Ls2 { get; set; }
            public double Rms { get; set; }
            public double Lss { get; set; }
            public double RSq { get; set; }
            public double Score { get; set; }
            public List<string> Columns { get; set; }
        }

        public ITransformer Model { get; set; }

        public PredictionEngine<Data, Prediction> PredictionEngine { get; set; }

        public string ModelName { get; set; }

        public string PredictColumn { get; set; }

        public Metric CurrentMetric { get; set; }

            public void CloseModel()
            {
                Model = null;
                ModelName = string.Empty;
            }

            private List<string> GetColumns(string datasetFileName, string predictColumn)
            {
                var columns = new List<string>();

                string[] headerSplit;

                using (var file = new System.IO.StreamReader(datasetFileName))
                {
                    file.ReadLine();
                    var header = file.ReadLine();
                    headerSplit = header.Split(',');
                }

                var mappedPredictColumn = string.Empty;

                for (var i = 0; i < headerSplit.Length; i++)
                {
                    var columnName = string.Empty;
                    if (headerSplit[i] == predictColumn)
                        columnName = $"Label";
                    else
                        columnName = $"F{i}";

                    columns.Add($"{columnName};{headerSplit[i]}");
                }

                return columns;
            }

            private double ReverseTransform(double min, double max, double transformed)
            {
                return ((max - min) * transformed) + min;
            }

            private ITransformer GetModel(string[] columns, out IEstimator<ITransformer> trainingPipeline)
            {
                var featureColumns = columns.Select(x => x.Split(";")[0]).ToArray();
                var dataProcessPipeline = mlContext.Transforms.Concatenate("Features", featureColumns);
                //var trainer = mlContext.Regression.Trainers.FastTree(labelColumnName: "Label", featureColumnName: "Features");
                //var trainer = mlContext.Regression.Trainers.FastTree(new FastTreeRegressionTrainer.Options() { NumberOfLeaves = 111, MinimumExampleCountPerLeaf = 10, NumberOfTrees = 500, LearningRate = 0.09443268f, Shrinkage = 0.1181826f, LabelColumnName = "Label", FeatureColumnName = "Features" });
                var trainer = mlContext.Regression.Trainers.LightGbm(new LightGbmRegressionTrainer.Options() { NumberOfIterations = 100, LearningRate = 0.1885655f, NumberOfLeaves = 39, MinimumExampleCountPerLeaf = 10, UseCategoricalSplit = false, HandleMissingValue = false, UseZeroAsMissingValue = true, MinimumExampleCountPerGroup = 200, MaximumCategoricalSplitPointCount = 16, CategoricalSmoothing = 1, L2CategoricalRegularization = 10, Booster = new GradientBooster.Options() { L2Regularization = 0.5, L1Regularization = 0 }, LabelColumnName = "Label", FeatureColumnName = "Features" });
                trainingPipeline = dataProcessPipeline.Append(trainer);

                //https://medium.com/machinelearningadvantage/detect-text-sentiment-with-an-lstm-neural-network-in-c-e0576190487e
                //var features = NetUtil.Var(new int[] { 1 }, CNTK.DataType.Float);
                //var labels = NetUtil.Var(new int[] { 1 }, CNTK.DataType.Float,
                //    dynamicAxes: new List<CNTK.Axis>() { CNTK.Axis.DefaultBatchAxis() });

                //var lstmUnits = 32;
                //var network = features
                //    .OneHotOp(10000, true)
                //    .Embedding(32)
                //    .LSTM(lstmUnits, lstmUnits)
                //    .Dense(1, CNTKLib.Sigmoid)
                //    .ToNetwork();
                //Console.WriteLine("Model architecture:");

                return trainingPipeline.Fit(trainingDataView);

                //var dataProcessPipeline = mlContext.Transforms.Concatenate("Features", new[] { "Col1", "Col2" })
                //              .Append(mlContext.Transforms.NormalizeMinMax("Features", "Features"));
                //var trainer = mlContext.Regression.Trainers.Ols(labelColumnName: "Col3", featureColumnName: "Features");





            }

            private Metric GetMetric(IDataView trainingDataView, IEstimator<ITransformer> trainingPipeline, string[] columns)
            {
                var crossValidationResults = mlContext.Regression.CrossValidate(trainingDataView, trainingPipeline, numberOfFolds: 5, labelColumnName: "Label");
                var metric = GetMetric(crossValidationResults);
                metric.Score = (1 - metric.MAE) + (1 - metric.Ls2) + metric.RSq;
                if (metric.RSq == 0) metric.Score = 0;
                metric.Columns.AddRange(columns);
                return metric;
            }

            public class ModelInput
            {
                [ColumnName("col0"), LoadColumn(0)]
                public float Col0 { get; set; }


                [ColumnName("col1"), LoadColumn(1)]
                public float Col1 { get; set; }


                [ColumnName("col2"), LoadColumn(2)]
                public float Col2 { get; set; }


                [ColumnName("col3"), LoadColumn(3)]
                public float Col3 { get; set; }


                [ColumnName("col4"), LoadColumn(4)]
                public float Col4 { get; set; }


                [ColumnName("col5"), LoadColumn(5)]
                public float Col5 { get; set; }


                [ColumnName("col6"), LoadColumn(6)]
                public float Col6 { get; set; }


                [ColumnName("col7"), LoadColumn(7)]
                public float Col7 { get; set; }


                [ColumnName("col8"), LoadColumn(8)]
                public float Col8 { get; set; }


                [ColumnName("col9"), LoadColumn(9)]
                public float Col9 { get; set; }


                [ColumnName("col10"), LoadColumn(10)]
                public float Col10 { get; set; }


                [ColumnName("col11"), LoadColumn(11)]
                public float Col11 { get; set; }


            }


            public static IEstimator<ITransformer> BuildTrainingPipeline(MLContext mlContext)
            {
                // Data process configuration with pipeline data transformations 
                var dataProcessPipeline = mlContext.Transforms.Concatenate("Features", new[] { "F0", "F2", "F3", "F4", "F5", "F6", "F9" });
                // Set the training algorithm 
                var trainer = mlContext.Regression.Trainers.FastTree(labelColumnName: "Label", featureColumnName: "Features");

                var trainingPipeline = dataProcessPipeline.Append(trainer);

                return trainingPipeline;
            }

            //public static ITransformer TrainModel(MLContext mlContext, IDataView trainingDataView, IEstimator<ITransformer> trainingPipeline)
            //{
            //    Console.WriteLine("=============== Training  model ===============");

            //    ITransformer model = trainingPipeline.Fit(trainingDataView);

            //    Console.WriteLine("=============== End of training process ===============");
            //    return model;
            //}

            private static void Evaluate(MLContext mlContext, IDataView trainingDataView, IEstimator<ITransformer> trainingPipeline)
            {
                // Cross-Validate with single dataset (since we don't have two datasets, one for training and for evaluate)
                // in order to evaluate and get the model's accuracy metrics
                Console.WriteLine("=============== Cross-validating to get model's accuracy metrics ===============");
                var crossValidationResults = mlContext.Regression.CrossValidate(trainingDataView, trainingPipeline, numberOfFolds: 5, labelColumnName: "col11");
                PrintRegressionFoldsAverageMetrics(crossValidationResults);
            }

            private static void SaveModel(MLContext mlContext, ITransformer mlModel, string modelRelativePath, DataViewSchema modelInputSchema)
            {
                // Save/persist the trained model to a .ZIP file
                Console.WriteLine($"=============== Saving the model  ===============");
                mlContext.Model.Save(mlModel, modelInputSchema, modelRelativePath);
                Console.WriteLine("The model is saved to {0}", modelRelativePath);
            }

            //public static string GetAbsolutePath(string relativePath)
            //{
            //    FileInfo _dataRoot = new FileInfo(typeof(Program).Assembly.Location);
            //    string assemblyFolderPath = _dataRoot.Directory.FullName;

            //    string fullPath = Path.Combine(assemblyFolderPath, relativePath);

            //    return fullPath;
            //}

            public static void PrintRegressionMetrics(RegressionMetrics metrics)
            {
                Console.WriteLine($"*************************************************");
                Console.WriteLine($"*       Metrics for Regression model      ");
                Console.WriteLine($"*------------------------------------------------");
                Console.WriteLine($"*       LossFn:        {metrics.LossFunction:0.##}");
                Console.WriteLine($"*       R2 Score:      {metrics.RSquared:0.##}");
                Console.WriteLine($"*       Absolute loss: {metrics.MeanAbsoluteError:#.##}");
                Console.WriteLine($"*       Squared loss:  {metrics.MeanSquaredError:#.##}");
                Console.WriteLine($"*       RMS loss:      {metrics.RootMeanSquaredError:#.##}");
                Console.WriteLine($"*************************************************");
            }

            public static void PrintRegressionFoldsAverageMetrics(IEnumerable<TrainCatalogBase.CrossValidationResult<RegressionMetrics>> crossValidationResults)
            {
                var L1 = crossValidationResults.Select(r => r.Metrics.MeanAbsoluteError);
                var L2 = crossValidationResults.Select(r => r.Metrics.MeanSquaredError);
                var RMS = crossValidationResults.Select(r => r.Metrics.RootMeanSquaredError);
                var lossFunction = crossValidationResults.Select(r => r.Metrics.LossFunction);
                var R2 = crossValidationResults.Select(r => r.Metrics.RSquared);

                Console.WriteLine($"*************************************************************************************************************");
                Console.WriteLine($"*       Metrics for Regression model      ");
                Console.WriteLine($"*------------------------------------------------------------------------------------------------------------");
                Console.WriteLine($"*       Average L1 Loss:       {L1.Average():0.###} ");
                Console.WriteLine($"*       Average L2 Loss:       {L2.Average():0.###}  ");
                Console.WriteLine($"*       Average RMS:           {RMS.Average():0.###}  ");
                Console.WriteLine($"*       Average Loss Function: {lossFunction.Average():0.###}  ");
                Console.WriteLine($"*       Average R-squared:     {R2.Average():0.###}  ");
                Console.WriteLine($"*************************************************************************************************************");
            }


            public class ModelOutput
            {
                public float Score { get; set; }
            }

            //private static Lazy<PredictionEngine<ModelInput, ModelOutput>> PredictionEngine2 = new Lazy<PredictionEngine<ModelInput, ModelOutput>>(CreatePredictionEngine);

            public static PredictionEngine<ModelInput, ModelOutput> CreatePredictionEngine()
            {
                // Create new MLContext
                MLContext mlContext = new MLContext();

                // Load model & create prediction engine
                string modelPath = @"C:\Users\tycta\AppData\Local\Temp\MLVSTools\ConsoleApp16ML\ConsoleApp16ML.Model\MLModel.zip";
                ITransformer mlModel = mlContext.Model.Load(modelPath, out var modelInputSchema);
                var predEngine = mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(mlModel);

                return predEngine;
            }

            public bool TrainModel(string dataset, string predictColumn, string features)
            {
                var retval = false;

                try
                {
                    var datasetFileName = $@".\{dataset}.csv";

                    var started = DateTime.Now;

                    if (File.Exists(datasetFileName))
                    {
                        metricList = new List<Metric>();

                        if (Model != null) CloseModel();

                        var columns = GetColumns(datasetFileName, predictColumn);
                        var predictColumnIdx = columns.IndexOf(columns.Single(x => x.StartsWith("Label;")));
                        var columnIndex = columns.Where(x => !x.StartsWith("Label;")).Select(x => columns.IndexOf($"{x}"));

                        var dataColumns2 = new List<TextLoader.Column>();
                        for (var i = 0; i < columns.Count; i++)
                        {
                            dataColumns2.Add(new TextLoader.Column(columns[i].Split(";")[0], DataKind.Single, i));
                        }

                        M(enmMessageType.Trace, $"Loaded dataset [{dataset}]");

                        ITransformer model = null;
                        List<int[]> processingColumns2 = new List<int[]>();
                        List<string[]> processingColumns = new List<string[]>();

                        if (string.IsNullOrEmpty(features))
                        {
                            for (var i = Convert.ToInt32(columns.Count / 2); i < (columns.Count - 1); i++)
                            {
                                //var combinations = columnIndex.Where(x => !x.StartsWith("Label")).DifferentCombinations<string>(i);
                                var combinations = columnIndex.DifferentCombinations<int>(i);
                                M(enmMessageType.NoLine, ".");

                                foreach (var combination in combinations)
                                {
                                    var featureColumns = combination.ToArray<int>();
                                    processingColumns2.Add(featureColumns);
                                }
                            }
                        }
                        else if (features == "-c")
                        {
                            processingColumns.Add(columns.Where(x => !x.StartsWith("Label")).ToArray());
                        }
                        else
                        {
                            var splitFeatures = features.Split(",");
                            var tempColumns = new List<string>();
                            for (var i = 0; i < splitFeatures.Length; i++)
                            {
                                var feature = columns.Where(x => x.EndsWith($";{splitFeatures[i]}")).ToList()[0].Split(";")[0];
                                tempColumns.Add($"{feature};{splitFeatures[i]}");
                            }

                            processingColumns.Add(tempColumns.ToArray());
                        }

                        //https://docs.microsoft.com/en-us/dotnet/api/microsoft.ml.dataoperationscatalog.loadfromenumerable?view=ml-dotnet
                        //https://docs.microsoft.com/en-us/dotnet/machine-learning/how-does-mldotnet-work

                        var lines = GetLines(datasetFileName);

                        //trainingDataView = mlContext.Data.LoadFromTextFile(
                        //                path: datasetFileName,
                        //                hasHeader: true,
                        //                columns: dataColumns.ToArray(),
                        //                separatorChar: ',',
                        //                allowQuoting: true,
                        //                allowSparse: false);

                        var schemaDef = SchemaDefinition.Create(typeof(Data));
                        trainingDataView = mlContext.Data.LoadFromEnumerable(lines, schemaDef);

                        if (processingColumns.Count > 1) M(enmMessageType.Trace, $"Combinations = {processingColumns.Count}");

                        var rSqAvg = 0d;

                        for (var i = 0; i < processingColumns2.Count; i++)
                        {
                            var combination = processingColumns2[i];

                            IEstimator<ITransformer> trainingPipeline;

                            var columnNames = combination.Select(x => columns[x]).ToArray();
                            model = GetModel(columnNames, out trainingPipeline);

                            var metric = GetMetric(trainingDataView, trainingPipeline, columnNames);
                            metricList.Add(metric);

                            M(enmMessageType.Trace, $"{String.Join(",", combination)} > L1={metric.MAE:0.###}, L2={metric.Ls2:0.###}, Rms={metric.Rms:0.###}, Loss={metric.Lss:0.###}, RSq={metric.RSq:0.###}, Scr={metric.Score:0.###}");

                            rSqAvg += metric.RSq;

                            if (i % 20 == 0 && i != 0)
                            {
                                var running = DateTime.Now.Subtract(started);
                                var toGo = GetDifference(running, i, processingColumns.Count);
                                M(enmMessageType.Info, $"Running: {(running.Hours == 1 ? running.Hours + " hour " : "")}{(running.Hours > 1 ? running.Hours + " hours " : "")}{(running.Minutes == 1 ? running.Minutes + " minute " : "")}{(running.Minutes > 1 ? running.Minutes + " minutes " : "")}{(running.Seconds == 1 ? running.Seconds + " second" : "")}{(running.Seconds > 1 ? running.Seconds + " seconds" : "")}");
                                M(enmMessageType.Info, $"Left: {(toGo.Hours == 1 ? toGo.Hours + " hour " : "")}{(toGo.Hours > 1 ? toGo.Hours + " hours " : "")}{(toGo.Minutes == 1 ? toGo.Minutes + " minute " : "")}{(toGo.Minutes > 1 ? toGo.Minutes + " minutes " : "")}{(toGo.Seconds == 1 ? toGo.Seconds + " second" : "")}{(toGo.Seconds > 1 ? toGo.Seconds + " seconds" : "")}");
                                var total = running.Add(toGo);
                                M(enmMessageType.Info, $"Total: {(total.Hours == 1 ? total.Hours + " hour " : "")}{(total.Hours > 1 ? total.Hours + " hours " : "")}{(total.Minutes == 1 ? total.Minutes + " minute " : "")}{(total.Minutes > 1 ? total.Minutes + " minutes " : "")}{(total.Seconds == 1 ? total.Seconds + " second" : "")}{(total.Seconds > 1 ? total.Seconds + " seconds" : "")}");

                                var best = metricList.OrderBy(x => x.RSq).Last();
                                M(enmMessageType.Trace, $"Batch #{(i / 20):0} It={i} Avg={rSqAvg / i:0.###} Rsq={best.RSq:0.###}, Columns={String.Join(",", combination)}");
                            }

                            if (P())
                            {
                                //if (C("Do you want to exit, type in 'yes' to exit?", "yes")) 
                                break;
                            }
                        }

                        if (metricList.Count > 0)
                        {
                            var topList = metricList.OrderByDescending(x => x.RSq).Take(20).OrderBy(x => x.RSq);
                            var bestMetric = topList.Last();

                            foreach (var top in topList)
                            {
                                M(enmMessageType.Trace, $"{(bestMetric.Equals(top) ? "BEST" : "TOP")} {String.Join(",", top.Columns)} > L1={top.MAE:0.###}, L2={top.Ls2:0.###}, Rms={top.Rms:0.###}, Loss={top.Lss:0.###}, RSq={top.RSq:0.###}, Scr={top.Score:0.###}");
                            }

                            var running = DateTime.Now.Subtract(started);
                            M(enmMessageType.Info, $"Completed: {(running.Hours == 1 ? running.Hours + " hour " : "")}{(running.Hours > 1 ? running.Hours + " hours " : "")}{(running.Minutes == 1 ? running.Minutes + " minute " : "")}{(running.Minutes > 1 ? running.Minutes + " minutes " : "")}{(running.Seconds == 1 ? running.Seconds + " second" : "")}{(running.Seconds > 1 ? running.Seconds + " seconds" : "")}");

                            IEstimator<ITransformer> trainingPipeline;
                            model = GetModel(topList.Last().Columns.ToArray(), out trainingPipeline);

                            PredictionEngine = mlContext.Model.CreatePredictionEngine<Data, Prediction>(model, ignoreMissingColumns: false);
                            this.trainingPipeline = trainingPipeline;
                            PredictColumn = predictColumn;
                            Model = model;
                            CurrentMetric = bestMetric;
                            ModelName = dataset;

                            //EvaluateModel(dataset);

                            retval = true;
                        }
                        else
                        {
                            Model = null;
                            PredictionEngine = null;
                            ModelName = string.Empty;
                        }
                    }
                    else M(enmMessageType.Error, $"ERROR, no dataset found");
                }
                catch (Exception ex)
                {
                    M(enmMessageType.Error, ex.Message);
                }

                return retval;
            }

            //public bool TrainModel(string dataset, string predictColumn, string features)
            //{
            //    var retval = false;

            //    try
            //    {
            //        var datasetFileName = $@".\{dataset}.csv";

            //        var started = DateTime.Now;

            //        if (File.Exists(datasetFileName))
            //        {
            //            metricList = new List<Metric>();

            //            if (Model != null) CloseModel();

            //            List<string> columns = GetColumns(datasetFileName, predictColumn);

            //            var dataColumns = new List<TextLoader.Column>();
            //            for (var i = 0; i < columns.Count; i++) {
            //                dataColumns.Add(new TextLoader.Column(columns[i].Split(";")[0], DataKind.Single, i));
            //            }
            //            //                              columns: dataColumns.ToArray(),

            //            trainingDataView = mlContext.Data.LoadFromTextFile<Data>(
            //                                            path: datasetFileName,
            //                                            hasHeader: true,
            //                                            separatorChar: ',',
            //                                            allowQuoting: true,
            //                                            allowSparse: false);

            //            M(enmMessageType.Trace, $"Loaded dataset [{dataset}]"); 

            //            ITransformer model = null;
            //            List<string[]> processingColumns = new List<string[]>();

            //            if (string.IsNullOrEmpty(features))
            //            {
            //                for (var i = Convert.ToInt32(columns.Count / 2); i < (columns.Count - 1); i++)
            //                {
            //                    var combinations = columns.Where(x => !x.StartsWith("label")).DifferentCombinations<string>(i);

            //                    foreach (var combination in combinations)
            //                    {
            //                        var featureColumns = combination.ToArray<string>();
            //                        processingColumns.Add(featureColumns);
            //                    }
            //                }
            //            } else
            //            {
            //                var splitFeatures = features.Split(",");
            //                var tempColumns = new List<string>();
            //                for (var i = 0; i < splitFeatures.Length; i++)
            //                {
            //                    var feature = columns.Where(x => x.EndsWith($";{splitFeatures[i]}")).ToList()[0].Split(";")[0];
            //                    tempColumns.Add($"{feature};{splitFeatures[i]}");
            //                }

            //                processingColumns.Add(tempColumns.ToArray());
            //            }

            //            M(enmMessageType.Trace, $"Combinations = {processingColumns.Count}");


            //            var rSqAvg = 0d;

            //            for (var i = 0; i < processingColumns.Count; i++)
            //            {
            //                var combination = processingColumns[i];

            //                IEstimator<ITransformer> trainingPipeline;
            //                model = GetModel(combination, out trainingPipeline);
            //                var metric = GetMetric(trainingDataView, trainingPipeline, combination);
            //                metricList.Add(metric);

            //                M(enmMessageType.Trace, $"{String.Join(",", combination)} > L1={metric.MAE:0.###}, L2={metric.Ls2:0.###}, Rms={metric.Rms:0.###}, Loss={metric.Lss:0.###}, RSq={metric.RSq:0.###}, Scr={metric.Score:0.###}");

            //                rSqAvg += metric.RSq;

            //                if (i % 50 == 0 && i != 0)
            //                {
            //                    var running = DateTime.Now.Subtract(started);
            //                    var toGo = GetDifference(running, i, processingColumns.Count);
            //                    M(enmMessageType.Info, $"Running: {(running.Hours == 1 ? running.Hours + " hour " : "")}{(running.Hours > 1 ? running.Hours + " hours " : "")}{(running.Minutes == 1 ? running.Minutes + " minute " : "")}{(running.Minutes > 1 ? running.Minutes + " minutes " : "")}{(running.Seconds == 1 ? running.Seconds + " second" : "")}{(running.Seconds > 1 ? running.Seconds + " seconds" : "")}");
            //                    M(enmMessageType.Info, $"Left: {(toGo.Hours == 1 ? toGo.Hours + " hour " : "")}{(toGo.Hours > 1 ? toGo.Hours + " hours " : "")}{(toGo.Minutes == 1 ? toGo.Minutes + " minute " : "")}{(toGo.Minutes > 1 ? toGo.Minutes + " minutes " : "")}{(toGo.Seconds == 1 ? toGo.Seconds + " second" : "")}{(toGo.Seconds > 1 ? toGo.Seconds + " seconds" : "")}");
            //                    var total = running.Add(toGo);
            //                    M(enmMessageType.Info, $"Total: {(total.Hours == 1 ? total.Hours + " hour " : "")}{(total.Hours > 1 ? total.Hours + " hours " : "")}{(total.Minutes == 1 ? total.Minutes + " minute " : "")}{(total.Minutes > 1 ? total.Minutes + " minutes " : "")}{(total.Seconds == 1 ? total.Seconds + " second" : "")}{(total.Seconds > 1 ? total.Seconds + " seconds" : "")}");

            //                    var best = metricList.OrderBy(x => x.RSq).Last();
            //                    M(enmMessageType.Trace, $"Batch #{(i / 50):0} It={i} Avg={rSqAvg / i:0.###} Rsq={best.RSq:0.###}, Columns={String.Join(",", combination)}");
            //                }

            //                if (B())
            //                {
            //                    //if (C("Do you want to exit, type in 'yes' to exit?", "yes")) 
            //                    break;
            //                }
            //            }

            //            if (metricList.Count > 0)
            //            {
            //                var topList = metricList.OrderByDescending(x => x.RSq).Take(20).OrderBy(x => x.RSq);
            //                var bestMetric = topList.Last();

            //                foreach (var top in topList)
            //                {
            //                    M(enmMessageType.Trace, $"{(bestMetric.Equals(top) ? "BEST" : "TOP")} {String.Join(",", top.Columns)} > L1={top.MAE:0.###}, L2={top.Ls2:0.###}, Rms={top.Rms:0.###}, Loss={top.Lss:0.###}, RSq={top.RSq:0.###}, Scr={top.Score:0.###}");
            //                }

            //                var running = DateTime.Now.Subtract(started);
            //                M(enmMessageType.Info, $"Completed: {(running.Hours == 1 ? running.Hours + " hour " : "")}{(running.Hours > 1 ? running.Hours + " hours " : "")}{(running.Minutes == 1 ? running.Minutes + " minute " : "")}{(running.Minutes > 1 ? running.Minutes + " minutes " : "")}{(running.Seconds == 1 ? running.Seconds + " second" : "")}{(running.Seconds > 1 ? running.Seconds + " seconds" : "")}");

            //                IEstimator<ITransformer> trainingPipeline;
            //                model = GetModel(topList.Last().Columns.ToArray(), out trainingPipeline);

            //                PredictionEngine = mlContext.Model.CreatePredictionEngine<Data, Prediction>(model, ignoreMissingColumns: false);
            //                this.trainingPipeline = trainingPipeline;
            //                PredictColumn = predictColumn;
            //                Model = model;
            //                CurrentMetric = bestMetric;
            //                ModelName = dataset;

            //                EvaluateModel(dataset);

            //                retval = true;
            //            }
            //            else
            //            {
            //                Model = null;
            //                PredictionEngine = null;
            //                ModelName = string.Empty;
            //            }
            //        }
            //        else M(enmMessageType.Error, $"ERROR, no dataset found");
            //    } catch (Exception ex)
            //    {
            //        M(enmMessageType.Error, ex.Message);
            //    }

            //    return retval;
            //}

            private TimeSpan GetDifference(TimeSpan running, int currentPosition, int totalPositions)
            {
                var batch = Math.Round((double)currentPosition / 50) + 1;
                var totalBatches = (totalPositions / 50);
                var diffMilliseconds = ((running.TotalMilliseconds / batch) * (totalBatches - batch));
                var diffTimeSpan = new TimeSpan((long)diffMilliseconds * TimeSpan.TicksPerMillisecond);

                return diffTimeSpan;
            }

            //public void RunModel(string dataset)
            //{
            //    if (Model != null)
            //    {
            //        var datasetFileName = $@".\{dataset}.csv";

            //        if (File.Exists(datasetFileName))
            //        {

            //        }
            //        else M(enmMessageType.Error, $"ERROR, no dataset found");
            //    }
            //    else M(enmMessageType.Error, $"ERROR, no model found");
            //}

            private void GetDataSet(string datasetFileName, out List<TextLoader.Column> dataColumns, out List<RecordInstrument> lines)
            {
                List<string> columns = GetColumns(datasetFileName, PredictColumn);

                dataColumns = new List<TextLoader.Column>();
                for (var i = 0; i < columns.Count; i++)
                {
                    dataColumns.Add(new TextLoader.Column(columns[i].Split(";")[0], DataKind.Single, i));
                }

                lines = new List<RecordInstrument>();
                using (var file = new System.IO.StreamReader(datasetFileName))
                {
                    var line = file.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                    {
                        line = file.ReadLine();
                        line = file.ReadLine();
                    }

                    while (!string.IsNullOrEmpty(line))
                    {
                        var o = new RecordInstrument();
                        o.Values = new Dictionary<string, string>();
                        var values = line.Split(",");

                        for (var i = 0; i < columns.Count; i++)
                        {
                            var column = columns[i];
                            if ((CurrentMetric == null) || CurrentMetric.Columns.Contains(column))
                            {
                                var splitColumn = column.Split(";");
                                o.Values.Add(splitColumn[1], values[i]);
                            }
                        }

                        if (columns.Contains($"Label;{PredictColumn}"))
                        {
                            o.Values.Add(PredictColumn, values[columns.IndexOf($"Label;{PredictColumn}")]);
                        }

                        lines.Add(o);

                        line = file.ReadLine();
                    };
                }
            }

            private List<Data> GetLines(string datasetFileName)
            {
                //List<string> columns = GetColumns(datasetFileName, PredictColumn);
                //dataColumns = new List<TextLoader.Column>();
                //for (var i = 0; i < columns.Count; i++)
                //{
                //    dataColumns.Add(new TextLoader.Column(columns[i].Split(";")[0], DataKind.Single, i));
                //}

                var lines = new List<Data>();
                using (var file = new System.IO.StreamReader(datasetFileName))
                {
                    var line = file.ReadLine();
                    line = file.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                    {
                        line = file.ReadLine();

                        while (!string.IsNullOrEmpty(line))
                        {
                            var data = new Data();
                            //var values = new List<string>();
                            var valueSplit = line.Split(",");
                            var dataType = typeof(Data);

                            for (var i = 0; i < valueSplit.Length; i++)
                            {
                                dataType.GetProperty("F" + i).SetValue(data, Convert.ToSingle(valueSplit[i]));
                            }

                            dataType.GetProperty("Label").SetValue(data, Convert.ToSingle(valueSplit[valueSplit.Length - 1]));

                            //foreach (var v in valueSplit)
                            //{
                            //    values.Add(v);
                            //}

                            //for (var i = 0; i < columns.Count; i++)
                            //{
                            //    var column = columns[i];
                            //    if ((CurrentMetric == null) || CurrentMetric.Columns.Contains(column))
                            //    {
                            //        var splitColumn = column.Split(";");
                            //        o.Values.Add(splitColumn[1], values[i]);
                            //    }
                            //}

                            //if (columns.Contains($"Label;{PredictColumn}"))
                            //{
                            //    o.Values.Add(PredictColumn, values[columns.IndexOf($"Label;{PredictColumn}")]);
                            //}

                            lines.Add(data);

                            line = file.ReadLine();
                        }
                    }
                }

                return lines;
            }


            public void RunModel(string dataset, int topPercent = 30)
            {
                if (Model != null)
                {
                    var datasetFileName = $@".\{dataset}.csv";

                    if (File.Exists(datasetFileName))
                    {
                        List<TextLoader.Column> dataColumns;
                        List<RecordInstrument> lines;

                        GetDataSet(datasetFileName, out dataColumns, out lines);

                        M(enmMessageType.Info, $"Loaded dataset [{dataset}]");

                        topPercent = (topPercent == 0 ? 70 : topPercent);
                        var actionPoint = 1 - ((1 / 100d) * topPercent);
                        var breakPoint = 1 - ((1 / 100d) * 50);

                        var err = 0;
                        var trn = 0;
                        var msd = 0;

                        var profit = 0d;
                        var previousPrice = 0d;
                        var action = "";
                        var actionCount = 0;

                        foreach (var record in lines)
                        {
                            var result = Predict(record);
                            var temp = Convert.ToSingle(record.Values["signal"].ToString());

                            if (result.Label < -actionPoint && (record.Values["signal"] != "-1"))
                            {
                                err += 1;
                            }
                            else if (result.Label > actionPoint && (record.Values["signal"] != "1"))
                            {
                                err += 1;
                            }
                            else if ((result.Label < -actionPoint || result.Label > actionPoint) && temp != 0)
                            {
                                if (result.Label < -actionPoint && string.IsNullOrEmpty(action))
                                {
                                    action = "sell";
                                    previousPrice = Convert.ToDouble(record.Values["offer+"].ToString());
                                    actionCount++;
                                }
                                else if (result.Label > actionPoint && string.IsNullOrEmpty(action))
                                {
                                    action = "buy";
                                    previousPrice = Convert.ToDouble(record.Values["offer+"].ToString());
                                    actionCount++;
                                }
                                else if (result.Label > -breakPoint && (action == "sell"))
                                {
                                    action = "";
                                    profit = previousPrice - Convert.ToDouble(record.Values["offer+"].ToString());
                                    actionCount++;
                                }
                                else if (result.Label < breakPoint && (action == "buy"))
                                {
                                    action = "";
                                    profit = Convert.ToDouble(record.Values["offer+"].ToString()) - previousPrice;
                                    actionCount++;
                                }

                                trn += 1;
                            }
                            else if (temp != 0)
                            {
                                msd += 1;
                            }
                        }

                        M(enmMessageType.Trace, $"TOTAL: Actions: {actionCount}, Profit: {profit}");
                    }
                    else M(enmMessageType.Error, $"ERROR, no dataset found");
                }
                else M(enmMessageType.Error, $"ERROR, no model found");
            }

            public void EvaluateModel(string dataset, int topPercent = 30)
            {
                if (Model != null)
                {
                    var datasetFileName = $@".\{dataset}.csv";

                    if (File.Exists(datasetFileName))
                    {
                        List<TextLoader.Column> dataColumns;
                        List<RecordInstrument> lines;

                        GetDataSet(datasetFileName, out dataColumns, out lines);

                        M(enmMessageType.Info, $"Loaded dataset [{dataset}]");

                        topPercent = (topPercent == 0 ? 70 : topPercent);
                        var breakPoint = 1 - ((1 / 100d) * topPercent);

                        var err = 0;
                        var trn = 0;
                        var msd = 0;
                        foreach (var record in lines)
                        {
                            var result = Predict(record);
                            var temp = Convert.ToSingle(record.Values["signal"].ToString());

                            if (result.Label < -breakPoint && (record.Values["signal"] != "-1"))
                            {
                                err += 1;
                            }
                            else if (result.Label > breakPoint && (record.Values["signal"] != "1"))
                            {
                                err += 1;
                            }
                            else if ((result.Label < -breakPoint || result.Label > breakPoint) && temp != 0)
                            {
                                trn += 1;
                            } else if (temp != 0)
                            {
                                msd += 1;
                            }
                        }

                        M(enmMessageType.Trace, $"PREDICTION: Errors: {err}, Success: {trn}, Missing: {msd}");

                        trainingDataView = mlContext.Data.LoadFromTextFile(
                                                        path: datasetFileName,
                                                        columns: dataColumns.ToArray(),
                                                        hasHeader: true,
                                                        separatorChar: ',',
                                                        allowQuoting: true,
                                                        allowSparse: false);

                        var model = GetModel(CurrentMetric.Columns.ToArray(), out this.trainingPipeline);

                        var metric = GetMetric(trainingDataView, this.trainingPipeline, CurrentMetric.Columns.ToArray());
                        M(enmMessageType.Trace, $"CURRENT:  L1={CurrentMetric.MAE:0.###}, L2={CurrentMetric.Ls2:0.###}, Rms={CurrentMetric.Rms:0.###}, Loss={CurrentMetric.Lss:0.###}, RSq={CurrentMetric.RSq:0.###}, Scr={CurrentMetric.Score:0.###}");
                        M(enmMessageType.Trace, $"EVALUATE: L1={metric.MAE:0.###}, L2={metric.Ls2:0.###}, Rms={metric.Rms:0.###}, Loss={metric.Lss:0.###}, RSq={metric.RSq:0.###}, Scr={metric.Score:0.###}");
                    }
                    else M(enmMessageType.Error, $"ERROR, no dataset found");
                } else M(enmMessageType.Error, $"ERROR, no model found");
            }

            public class Data
            {
                [LoadColumn(0), ColumnName("F0")]
                public float F0 { get; set; }

                [LoadColumn(1), ColumnName("F1")]
                public float F1 { get; set; }

                [LoadColumn(2), ColumnName("F2")]
                public float F2 { get; set; }

                [LoadColumn(3), ColumnName("F3")]
                public float F3 { get; set; }

                [LoadColumn(4), ColumnName("F4")]
                public float F4 { get; set; }

                [LoadColumn(5), ColumnName("F5")]
                public float F5 { get; set; }

                [LoadColumn(6), ColumnName("F6")]
                public float F6 { get; set; }

                [LoadColumn(7), ColumnName("F7")]
                public float F7 { get; set; }

                [LoadColumn(8), ColumnName("F8")]
                public float F8 { get; set; }

                [LoadColumn(9), ColumnName("F9")]
                public float F9 { get; set; }

                [LoadColumn(10), ColumnName("F10")]
                public float F10 { get; set; }


                [LoadColumn(11), ColumnName("F11")]
                public float F11 { get; set; }


                [LoadColumn(12), ColumnName("F12")]
                public float F12 { get; set; }


                [LoadColumn(13), ColumnName("F13")]
                public float F13 { get; set; }


                [LoadColumn(14), ColumnName("F14")]
                public float F14 { get; set; }


                [LoadColumn(15), ColumnName("F15")]
                public float F15 { get; set; }


                [LoadColumn(16), ColumnName("F16")]
                public float F16 { get; set; }


                [LoadColumn(17), ColumnName("F17")]
                public float F17 { get; set; }


                [LoadColumn(18), ColumnName("F18")]
                public float F18 { get; set; }


                [LoadColumn(19), ColumnName("F19")]
                public float F19 { get; set; }


                [LoadColumn(20), ColumnName("F20")]
                public float F20 { get; set; }

                [LoadColumn(21), ColumnName("F21")]
                public Single F21 { get; set; }

                [LoadColumn(22), ColumnName("F22")]
                public Single F22 { get; set; }

                [LoadColumn(23), ColumnName("F23")]
                public Single F23 { get; set; }

                [LoadColumn(24), ColumnName("F24")]
                public Single F24 { get; set; }

                [LoadColumn(25), ColumnName("F25")]
                public Single F25 { get; set; }

                [LoadColumn(26), ColumnName("F26")]
                public Single F26 { get; set; }

                [LoadColumn(27), ColumnName("F27")]
                public Single F27 { get; set; }

                [LoadColumn(28), ColumnName("F28")]
                public Single F28 { get; set; }

                [LoadColumn(29), ColumnName("F29")]
                public Single F29 { get; set; }

                [LoadColumn(30), ColumnName("Label")]
                public Single Label { get; set; }
            }

            public class Prediction
            {
                [ColumnName("Score")]
                public float Label { get; set; }
            }

            private Data GetData(RecordInstrument record)
            {
                var retval = new Data();
                var dataType = typeof(Data);

                for (var i = 0; i < CurrentMetric.Columns.Count; i++)
                {
                    var splitColumn = CurrentMetric.Columns[i].Split(";");
                    dataType.GetProperty(splitColumn[0]).SetValue(retval, Single.Parse(record.Values[splitColumn[1]]));
                }

                return retval;
            }

            public Prediction Predict(RecordInstrument record)
            {
                Prediction retval = null;

                if (Model != null)
                {
                    var data = GetData(record);

                    retval = PredictionEngine.Predict(data);
                }

                return retval;
            }

            public void LoadModel(string model)
            {
                var modelFileName = $@".\{model}.zip";

                if (File.Exists(modelFileName))
                {
                    if (Model != null) CloseModel();

                    MLContext mlContext = new MLContext();
                    Model = mlContext.Model.Load(modelFileName, out predictionPipelineSchema);
                    PredictionEngine = mlContext.Model.CreatePredictionEngine<Data, Prediction>(Model);

                    using (ZipArchive archive = ZipFile.OpenRead(modelFileName))
                    {
                        var infoFile = archive.GetEntry("info.json");
                        var json = string.Empty;

                        using (StreamReader reader = new StreamReader(infoFile.Open()))
                        {
                            json = reader.ReadToEnd();
                        }

                        var info = JObject.Parse(json);
                        PredictColumn = info["predictcolumn"].ToString();
                        ModelName = info["modelname"].ToString();
                        CurrentMetric = (Metric)JsonConvert.DeserializeObject(info["metric"].ToString(), typeof(Metric));
                    }

                    M(enmMessageType.Info, $"Loaded model [{model}]");
                }
                else M(enmMessageType.Info, $"ERROR, no model found");
            }

            public void SaveModel(string model)
            {
                M(enmMessageType.Info, $"Saving model [{model}]");

                var modelFilePath = $@".\{model}.zip";
                mlContext.Model.Save(Model, trainingDataView.Schema, modelFilePath);

                string json = $"{{predictcolumn: '{PredictColumn}', metric: '{JsonConvert.SerializeObject(CurrentMetric)}', modelname: '{ModelName}'}}";
                var info = JObject.Parse(json);

                using (ZipArchive archive = ZipFile.Open(modelFilePath, ZipArchiveMode.Update))
                {
                    var infoFile = archive.CreateEntry("info.json");
                    using (StreamWriter writer = new StreamWriter(infoFile.Open()))
                    {
                        writer.WriteLine(info);
                    }
                }

                M(enmMessageType.Info, "Model has been saved");
            }

            private Metric GetMetric(IEnumerable<TrainCatalogBase.CrossValidationResult<RegressionMetrics>> crossValidationResults)
            {
                var metric = new Metric();

                metric.MAE = crossValidationResults.Select(r => r.Metrics.MeanAbsoluteError).Average();
                metric.Ls2 = crossValidationResults.Select(r => r.Metrics.MeanSquaredError).Average();
                metric.Rms = crossValidationResults.Select(r => r.Metrics.RootMeanSquaredError).Average();
                metric.Lss = crossValidationResults.Select(r => r.Metrics.LossFunction).Average();
                metric.RSq = crossValidationResults.Select(r => r.Metrics.RSquared).Average();
                if (double.IsInfinity(metric.RSq)) metric.RSq = 0;

                metric.Columns = new List<string>();

                return metric;
            }
    }
}
