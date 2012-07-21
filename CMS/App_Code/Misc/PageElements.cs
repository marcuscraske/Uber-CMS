﻿/*
 * UBERMEAT FOSS
 * ****************************************************************************************
 * License:                 Creative Commons Attribution-ShareAlike 3.0 unported
 *                          http://creativecommons.org/licenses/by-sa/3.0/
 * 
 * Project:                 Uber CMS
 * File:                    /App_Code/Misc/PageElements.cs
 * Author(s):               limpygnome						limpygnome@gmail.com
 * To-do/bugs:              none
 * 
 * Responsible for handling the template engine by replacing markup within HTML with
 * set elements or changed based on state conditions.
 */
using System;
using System.Collections.Generic;
using System.Web;
using System.Text;
using System.Text.RegularExpressions;

namespace UberCMS.Misc
{
    /// <summary>
    /// Used to replace markup within a page with values for e.g. templates.
    /// 
    /// Markup example
    /// ******************
    /// If we had the following text:
    /// "It's a prutty day, <!--HELLO_WORLD-->!"
    /// 
    /// And then we added the following page-element:
    /// key - "HELLO_WORLD"
    /// value - "it works"
    /// 
    /// If we passed our text to the method replaceElements, the returned text would be:
    /// "It's a prutty day, it works!"
    /// </summary>
    public class PageElements
    {
        #region "Variables"
        private List<string> flags;
        private Dictionary<string, string> elements;
        #endregion

        #region "Methods - Constructor"
        public PageElements()
        {
            elements = new Dictionary<string, string>();
            flags = new List<string>();
        }
        #endregion

        #region "Methods - Mutators/Accessors"
        /// <summary>
        /// Fetches the value of a page-element.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string this[string key]
        {
            get
            {
                return elements.ContainsKey(key) ? elements[key] : null;
            }
            set
            {
                lock (elements)
                {
                    if (elements.ContainsKey(key))
                        elements[key] = value;
                    else
                        elements.Add(key, value);
                }
            }
        }
        #endregion

        #region "Methods"
        /// <summary>
        /// Replaces the page-element markup within a piece of text with the values in the collection; if a key does not exist, the value is replaced with empty.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="currTree"></param>
        /// <param name="treeMax"></param>
        public void replaceElements(ref StringBuilder text, int currTree, int treeMax)
        {
            // Find if-statements
            bool expression;
            MatchCollection elseMC;
            foreach (Match m in Regex.Matches(text.ToString(), @"<!--IF:([a-zA-Z0-9!_]*)-->(.*?)<!--ENDIF(:\1)?-->", RegexOptions.Singleline))
            {
                expression = (m.Groups[1].Value.StartsWith("!") && m.Groups[1].Value.Length > 1 && !flags.Contains(m.Groups[1].Value.Substring(1))) || flags.Contains(m.Groups[1].Value);
                elseMC = Regex.Matches(m.Groups[2].Value, @"(.*?)<!--ELSE-->(.*$?)", RegexOptions.Singleline);
                if (elseMC.Count == 1)
                {
                    if (expression)
                        text.Replace(m.Value, elseMC[0].Groups[1].Value);
                    else
                        text.Replace(m.Value, elseMC[0].Groups[2].Value);
                }
                else
                {
                    if (expression)
                        text.Replace(m.Value, m.Groups[2].Value);
                    else
                        text.Replace(m.Value, string.Empty);
                }
            }
            // Find replacement tags
            foreach (Match m in Regex.Matches(text.ToString(), @"<!--([a-zA-Z0-9_]*)-->"))
                text.Replace(m.Value, elements.ContainsKey(m.Groups[1].Value) ? elements[m.Groups[1].Value] : string.Empty);
            currTree++;
            if (currTree < treeMax) replaceElements(ref text, currTree, treeMax);
        }
        /// <summary>
        /// Flags are used for conditional blocks within templates to hide/show content based on flags being set.
        /// 
        /// Example syntax within template:
        /// 
        /// <!--IF:FLAG-->
        /// 
        /// <!--ENDIF-->
        /// 
        /// With NOT operator:
        /// 
        /// <!--IF:!FLAG-->
        /// 
        /// <!--ENDIF-->
        /// 
        /// With else tag:
        /// <!--IF:FLAG-->
        ///    true
        /// <!--ELSE-->
        ///    false
        /// <!--ENDIF-->
        /// 
        /// You can also use nested tags by changing the end-if tag:
        /// <!--IF:FLAG_A-->
        ///     <!--IF:FLAG_B-->
        ///         test
        ///     <!--ELSE-->
        ///         test
        ///     <!--ENDIF:FLAG_B-->
        /// <!--ENDIF:FLAG_A-->
        /// </summary>
        /// <param name="flag"></param>
        public void setFlag(string flag)
        {
            lock(flags)
                flags.Add(flag);
        }
        /// <summary>
        ///  Removes a flag.
        /// </summary>
        /// <param name="flag"></param>
        public void removeFlag(string flag)
        {
            lock(flags)
                if (flags.Contains(flag))
                    flags.Remove(flag);
        }
        /// <summary>
        /// Specifies if a flag exists in the collection.
        /// </summary>
        /// <param name="flag"></param>
        /// <returns></returns>
        public bool containsFlag(string flag)
        {
            return flags.Contains(flag);
        }
        /// <summary>
        /// Specifies if an element key exists in the collection.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool containsElementKey(string key)
        {
            return elements.ContainsKey(key);
        }
        #endregion
    }
}