﻿namespace Plant.Model
{
    /// <summary>
    /// Represents a data provider manager
    /// </summary>
    public partial interface IDataProviderManager
    {
        #region Properties

        /// <summary>
        /// Gets data provider
        /// </summary>
        IPlantDataProvider DataProvider { get; }

        #endregion
    }
}
