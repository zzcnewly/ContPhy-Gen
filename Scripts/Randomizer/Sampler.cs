using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace SamplingFunctions{
public class Sampler
{
    public const int CONRANGE = 1;
    public const int DISRANGE = 2;
    public const int CHOICE = 3;
    public const int SELFDEFINE = 4;
    public const int GAUSS = 5;
    public const int EXP = 6;
    public const int MULTICHOICE = 7;
    public Random random;
    public Sampler(int seed)
    {
        random = new Random(seed);
    }

    public double Sample_Conrange(List<double> distr)
    {
        // input --- List of [1.2, 3.4, 1.2, 3.4]...
        double value = random.NextDouble() * (double)(distr[1] - distr[0]) + (double)distr[0];
        if (distr.Count == 4)
        {
            value = Math.Max(value, (double)distr[2]);
            value = Math.Min(value, (double)distr[3]);
        }
        return value;
    }

    public int Sample_Choice(List<int> distr)
    {
        // distr --- List of ["123", "456", "789", ...] or [123, 456, 789, ...] an so on...
        return (distr[random.Next(distr.Count)]);
    }
    public string Sample_Choice(List<string> distr)
    {
        // distr --- List of ["123", "456", "789", ...] or [123, 456, 789, ...] an so on...
        return (distr[random.Next(distr.Count)]);
    }
    public double Sample_Choice(List<double> distr)
    {
        // distr --- List of ["123", "456", "789", ...] or [123, 456, 789, ...] an so on...
        return (distr[random.Next(distr.Count)]);
    }

    public double Sample_Gauss(List<double> distr)
    {
        // input --- List of [miu1.2, sigma3.4, min1.2, max3.4]...
        double r1= random.NextDouble();
        double r2= random.NextDouble();
        double value = Math.Sqrt(-2.0*Math.Log(r1))*Math.Cos(2.0*Math.PI*r2)* (double)distr[1] + (double)distr[0];
        if (distr.Count == 4)
        {
            value = Math.Max(value, (double)distr[2]);
            value = Math.Min(value, (double)distr[3]);
        }
        return value;
    }

    public double Sample_Exp(List<double> distr)
    {
        // input ---- first log and uniformly sampled then exp output 
        double value = random.NextDouble() * (Math.Log((double)distr[1]) - Math.Log((double)distr[0])) + Math.Log((double)distr[0]);
        value = Math.Exp(value);
        if (distr.Count == 4)
        {
            value = Math.Max(value, (double)distr[2]);
            value = Math.Min(value, (double)distr[3]);
        }
        return value;
    }
            
    public List<int> Sample_MultiChoice(List<int> distr, int num)
    {
        // input ---- struct multichoice with value/List<T> and num/int
        List<int> list = new();
        list.AddRange(distr);

        // Shuffle the list randomly
        List<int> shuffledList = list.OrderBy(x => random.Next()).ToList();

        // Take the first n objects from the shuffled list
        List<int> selectedObjects = shuffledList.Take(num).ToList();
        return selectedObjects;
    }
    public List<string> Sample_MultiChoice(List<string> distr, int num)
    {
        // input ---- struct multichoice with value/List<T> and num/int
        List<string> list = new();
        list.AddRange(distr);

        // Shuffle the list randomly
        List<string> shuffledList = list.OrderBy(x => random.Next()).ToList();

        // Take the first n objects from the shuffled list
        List<string> selectedObjects = shuffledList.Take(num).ToList();
        return selectedObjects;
    }
    public List<double> Sample_MultiChoice(List<double> distr, int num)
    {
        // input ---- struct multichoice with value/List<T> and num/int
        List<double> list = new();
        list.AddRange(distr);

        // Shuffle the list randomly
        List<double> shuffledList = list.OrderBy(x => random.Next()).ToList();

        // Take the first n objects from the shuffled list
        List<double> selectedObjects = shuffledList.Take(num).ToList();
        return selectedObjects;
    }

    public List<double> Sample_MultipleInRangeBySteps(List<double> distr, int num){
        // input ---- struct multichoice with value/List<T> and num/int
        List<int> list = new();
        int num0 = (int)((distr[1] - distr[0]) / distr[2]);
        list.AddRange(Enumerable.Range(0, num0));
        // Shuffle the list randomly
        List<int> shuffledList = list.OrderBy(x => random.Next()).ToList();
        // Take the first n objects from the shuffled list
        List<int> selectedObjects = shuffledList.Take(num).ToList();
        List<double> returnlist = new();
        foreach (var item in selectedObjects)
        {
            returnlist.Add((double)((double)item * distr[2] + distr[0]));
        }
        return returnlist;
    }
    public List<int> Sample_MultipleInRangeBySteps(List<int> distr, int num){
        // input ---- struct multichoice with value/List<T> and num/int
        List<int> list = new();
        int num0 = (int)((distr[1] - distr[0]) / distr[2]);
        list.AddRange(Enumerable.Range(0, num0));
        // Shuffle the list randomly
        List<int> shuffledList = list.OrderBy(x => random.Next()).ToList();
        // Take the first n objects from the shuffled list
        List<int> selectedObjects = shuffledList.Take(num).ToList();
        List<int> returnlist = new();
        foreach (var item in selectedObjects)
        {
            returnlist.Add((int)(item * distr[2] + distr[0]));
        }
        return returnlist;
    }
}
}
           
